using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Microsoft.Extensions.Logging;

using Logging = UK.Gov.Legislation.Judgments.Logging;

namespace UK.Gov.Legislation.Common.Rendering {

public sealed class LocalSubprocessRenderer : IDrawingRenderer {

    private static readonly ILogger logger =
        Logging.Factory.CreateLogger<LocalSubprocessRenderer>();

    private readonly string sofficePath;
    private readonly TimeSpan conversionTimeout;
    private readonly SemaphoreSlim subprocessGate;

    public LocalSubprocessRenderer(
        string sofficePath, TimeSpan? conversionTimeout = null, int? maxConcurrency = null) {
        this.sofficePath = sofficePath
            ?? throw new ArgumentNullException(nameof(sofficePath));
        this.conversionTimeout = conversionTimeout ?? TimeSpan.FromMinutes(2);
        int limit = maxConcurrency ?? Environment.ProcessorCount;
        if (limit < 1) limit = 1;
        this.subprocessGate = new SemaphoreSlim(limit, limit);
    }

    public IReadOnlyList<byte[]> RenderAllDrawings(byte[] docx, CancellationToken ct) {
        if (docx == null || docx.Length == 0) return Array.Empty<byte[]>();
        if (!File.Exists(sofficePath)) {
            logger.LogWarning("soffice not found at {Path}; cannot render", sofficePath);
            return Array.Empty<byte[]>();
        }
        return ConvertAndExtract(docx, ct) ?? Array.Empty<byte[]>();
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
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add($"-env:UserInstallation={profileUri}");
        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--convert-to");
        psi.ArgumentList.Add("html");
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(inputDocx);

        subprocessGate.Wait(ct);
        try {
            using var proc = Process.Start(psi);
            if (proc == null) {
                logger.LogError("failed to start soffice at {Path}", sofficePath);
                return false;
            }
            // Drain stdout/stderr asynchronously so the pipe buffers don't fill and deadlock.
            var stderrBuf = new StringBuilder();
            var stdoutBuf = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) lock (stdoutBuf) stdoutBuf.AppendLine(e.Data); };
            proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) lock (stderrBuf) stderrBuf.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var reg = ct.Register(() => { try { proc.Kill(entireProcessTree: true); } catch { } });
            if (!proc.WaitForExit((int) conversionTimeout.TotalMilliseconds)) {
                try { proc.Kill(entireProcessTree: true); } catch { }
                logger.LogWarning("soffice did not complete within {Timeout}", conversionTimeout);
                return false;
            }
            if (proc.ExitCode != 0) {
                logger.LogWarning("soffice exited {Code}: {Err}", proc.ExitCode, stderrBuf.ToString());
                return false;
            }
            return true;
        } finally {
            subprocessGate.Release();
        }
    }

    private static readonly Regex TokenPattern = new(
        @"(?<marker>LEGRENDERMARK(?<idx>\d{5})ENDMARK)|(?<img><img\b[^>]*?\bsrc\s*=\s*[""'](?<src>[^""']+)[""'][^>]*>)",
        RegexOptions.Compiled);

    private IReadOnlyList<byte[]> ExtractImagesByMarkers(string htmlPath, int drawingCount) {
        string html = File.ReadAllText(htmlPath);
        string dir = Path.GetDirectoryName(htmlPath);

        var result = new byte[drawingCount][];
        int regionStart = -1;
        var regionImgs = new List<string>();
        int lastMarkerSeen = -1;

        void Flush(int regionEnd) {
            if (regionStart < 0) { regionImgs.Clear(); return; }
            int span = regionEnd - regionStart;
            if (regionImgs.Count > 0 && span > 1 && regionImgs.Count != span)
                logger.LogWarning(
                    "rendered-image count {Imgs} does not match drawing span [{Start},{End}); distributing across span",
                    regionImgs.Count, regionStart, regionEnd);
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
                lastMarkerSeen = idx;
                continue;
            }
            if (!m.Groups["img"].Success) continue;
            string src = m.Groups["src"].Value;
            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            if (src.Contains("://")) continue;
            regionImgs.Add(src);
        }
        Flush(drawingCount);

        if (lastMarkerSeen + 1 < drawingCount)
            logger.LogWarning(
                "expected {Expected} markers, only saw through {Seen}; rendered set is incomplete",
                drawingCount, lastMarkerSeen + 1);

        return result;
    }

}

}
