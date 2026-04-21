using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;

using Microsoft.Extensions.Logging;

using Logging = UK.Gov.Legislation.Judgments.Logging;

namespace UK.Gov.Legislation.Common.Rendering {

public sealed class LocalSubprocessRenderer : IDrawingRenderer {

    private static readonly ILogger logger =
        Logging.Factory.CreateLogger<LocalSubprocessRenderer>();

    private readonly string sofficePath;
    private readonly TimeSpan conversionTimeout;

    private readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<byte[]>>> imageCache = new();

    public LocalSubprocessRenderer(string sofficePath, TimeSpan? conversionTimeout = null) {
        this.sofficePath = sofficePath
            ?? throw new ArgumentNullException(nameof(sofficePath));
        this.conversionTimeout = conversionTimeout ?? TimeSpan.FromMinutes(2);
    }

    public byte[] TryRenderDrawing(byte[] docx, int drawingIndex, CancellationToken ct) {
        if (docx == null || docx.Length == 0) return null;
        if (!File.Exists(sofficePath)) {
            logger.LogWarning("soffice not found at {Path}; cannot render", sofficePath);
            return null;
        }

        string hash = HashDocx(docx);
        var lazy = imageCache.GetOrAdd(hash, _ => new Lazy<IReadOnlyList<byte[]>>(
            () => ConvertAndExtract(docx, ct) ?? Array.Empty<byte[]>(),
            System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));
        var images = lazy.Value;
        if (images == null || images.Count == 0) return null;
        if (drawingIndex < 0 || drawingIndex >= images.Count) return null;
        return images[drawingIndex];
    }

    private static string HashDocx(byte[] docx) {
        byte[] h = SHA256.HashData(docx);
        return Convert.ToHexString(h);
    }

    private IReadOnlyList<byte[]> ConvertAndExtract(byte[] docx, CancellationToken ct) {
        string workDir = Path.Combine(
            Path.GetTempPath(), $"leg_render_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        try {
            byte[] marked = DocxMarkerInjector.InjectDrawingMarkers(docx, out int drawingCount);
            if (drawingCount == 0) return Array.Empty<byte[]>();

            string input = Path.Combine(workDir, "input.docx");
            File.WriteAllBytes(input, marked);

            if (!ConvertToHtml(input, workDir, ct))
                return null;

            string html = Path.Combine(workDir, "input.html");
            if (!File.Exists(html)) {
                logger.LogWarning("soffice did not produce an HTML file");
                return null;
            }

            return ExtractImagesByMarkers(html, drawingCount);
        } finally {
            try { Directory.Delete(workDir, recursive: true); }
            catch (Exception ex) {
                logger.LogDebug(ex, "failed to clean up {WorkDir}", workDir);
            }
        }
    }

    private bool ConvertToHtml(string inputDocx, string outDir, CancellationToken ct) {
        // Per-invocation user profile so concurrent soffice processes don't collide on the default profile.
        string profileDir = Path.Combine(outDir, "profile");
        Directory.CreateDirectory(profileDir);
        string profileUri = new Uri(profileDir).AbsoluteUri;
        var psi = new ProcessStartInfo {
            FileName = sofficePath,
            Arguments = $"-env:UserInstallation={profileUri} --headless --convert-to html --outdir \"{outDir}\" \"{inputDocx}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi);
        if (proc == null) {
            logger.LogError("failed to start soffice at {Path}", sofficePath);
            return false;
        }
        using var reg = ct.Register(() => { try { proc.Kill(entireProcessTree: true); } catch { } });
        if (!proc.WaitForExit((int) conversionTimeout.TotalMilliseconds)) {
            try { proc.Kill(entireProcessTree: true); } catch { }
            logger.LogWarning("soffice did not complete within {Timeout}", conversionTimeout);
            return false;
        }
        if (proc.ExitCode != 0) {
            logger.LogWarning("soffice exited {Code}: {Err}",
                proc.ExitCode, proc.StandardError.ReadToEnd());
            return false;
        }
        return true;
    }

    private static readonly Regex TokenPattern = new(
        @"(?<marker>LEGRENDERMARK(?<idx>\d{5})ENDMARK)|(?<img><img\b[^>]*?\bsrc\s*=\s*[""'](?<src>[^""']+)[""'][^>]*>)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private IReadOnlyList<byte[]> ExtractImagesByMarkers(string htmlPath, int drawingCount) {
        string html = File.ReadAllText(htmlPath);
        string dir = Path.GetDirectoryName(htmlPath);

        var result = new byte[drawingCount][];
        int regionStart = -1;
        var regionImgs = new List<string>();

        void Flush(int regionEnd) {
            if (regionStart < 0) { regionImgs.Clear(); return; }
            int imgIdx = 0;
            for (int d = regionStart; d < regionEnd && d < drawingCount; d++) {
                if (imgIdx >= regionImgs.Count) break;
                if (result[d] != null) { imgIdx++; continue; }
                string src = regionImgs[imgIdx++];
                string resolved = Path.Combine(dir, src);
                if (!File.Exists(resolved)) continue;
                try { result[d] = File.ReadAllBytes(resolved); }
                catch (Exception ex) { logger.LogDebug(ex, "failed to read {Path}", resolved); }
            }
            regionImgs.Clear();
        }

        foreach (Match m in TokenPattern.Matches(html)) {
            if (m.Groups["marker"].Success) {
                if (!int.TryParse(m.Groups["idx"].Value, out int idx)) continue;
                Flush(idx);
                regionStart = idx;
                continue;
            }
            if (!m.Groups["img"].Success) continue;
            string src = m.Groups["src"].Value;
            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            if (src.Contains("://")) continue;
            regionImgs.Add(src);
        }
        Flush(drawingCount);
        return result;
    }

}

}
