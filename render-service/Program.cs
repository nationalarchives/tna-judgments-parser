using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Common.Rendering;

namespace UK.Gov.Legislation.RenderService;

public static class Program {

    public static void Main(string[] args) {
        BuildApp(args, Config.FromEnvironment()).Run();
    }

    public static WebApplication BuildApp(string[] args, Config config) {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(o => {
            // Request body is JSON only (URL string). Anything bigger is rejected before we touch it.
            o.Limits.MaxRequestBodySize = 64 * 1024;
        });

        builder.Services.AddSingleton(config);
        builder.Services.AddSingleton(new DocxToImageRenderer(
            config.SofficePath, TimeSpan.FromMilliseconds(config.RenderTimeoutMs), config.MaxConcurrency));
        builder.Services.AddSingleton(new HttpClient {
            Timeout = TimeSpan.FromMilliseconds(config.DownloadTimeoutMs),
            // Hard cap so a malicious origin can't stream forever and exhaust the timeout window.
            MaxResponseContentBufferSize = config.MaxDocxBytes,
        });
        builder.Services.AddSingleton(new HostAllowlist(config.AllowedDownloadHostPatterns));
        builder.Services.AddSingleton<SofficeProbe>();

        var app = builder.Build();
        var probe = app.Services.GetRequiredService<SofficeProbe>();
        probe.Refresh();  // one-shot at startup; /health reflects this until restart

        app.MapGet("/health", () =>
            probe.IsHealthy ? Results.Ok(new { status = "ok" })
                            : Results.Json(new { status = "soffice_unavailable" }, statusCode: 503));

        app.MapGet("/version", () => Results.Json(new {
            git_sha = AssemblyVersion(),
            built_at = AssemblyBuiltAt(),
        }));

        app.MapPost("/render", HandleRender);

        return app;
    }

    private static async Task<IResult> HandleRender(
            HttpContext ctx,
            DocxToImageRenderer renderer,
            HttpClient http,
            HostAllowlist allowlist,
            Config config,
            ILogger<Program_LoggerCategory> log,
            CancellationToken ct) {

        string requestId = Guid.NewGuid().ToString("N").Substring(0, 12);

        RenderRequest? body;
        try {
            body = await JsonSerializer.DeserializeAsync<RenderRequest>(
                ctx.Request.Body, JsonOpts, ct);
        } catch (JsonException ex) {
            log.LogWarning(ex, "[{Rid}] malformed JSON body", requestId);
            return Results.Json(new { error = "malformed_json" }, statusCode: 400);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.DocxUrl))
            return Results.Json(new { error = "missing_docx_url" }, statusCode: 400);

        if (!Uri.TryCreate(body.DocxUrl, UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
            return Results.Json(new { error = "invalid_url" }, statusCode: 400);
        }

        if (!allowlist.IsAllowed(uri.Host)) {
            log.LogWarning("[{Rid}] disallowed host {Host}", requestId, uri.Host);
            return Results.Json(new { error = "disallowed_host" }, statusCode: 400);
        }

        byte[] docx;
        try {
            // We never log the URL itself — presigned URLs carry credentials in the query.
            HttpResponseMessage resp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) {
                log.LogWarning("[{Rid}] upstream {Status} from host {Host}", requestId, (int) resp.StatusCode, uri.Host);
                return Results.Json(new { error = "upstream_failed", upstream_status = (int) resp.StatusCode }, statusCode: 502);
            }
            if (resp.Content.Headers.ContentLength is long cl && cl > config.MaxDocxBytes) {
                return Results.Json(new { error = "docx_too_large", limit = config.MaxDocxBytes }, statusCode: 413);
            }
            docx = await resp.Content.ReadAsByteArrayAsync(ct);
            if (docx.Length > config.MaxDocxBytes) {
                return Results.Json(new { error = "docx_too_large", limit = config.MaxDocxBytes }, statusCode: 413);
            }
        } catch (TaskCanceledException) when (!ct.IsCancellationRequested) {
            return Results.Json(new { error = "download_timeout" }, statusCode: 504);
        } catch (HttpRequestException ex) {
            log.LogWarning(ex, "[{Rid}] download failed from host {Host}", requestId, uri.Host);
            return Results.Json(new { error = "download_failed" }, statusCode: 502);
        }

        var sw = Stopwatch.StartNew();
        IReadOnlyList<byte[]> drawings;
        try {
            drawings = await Task.Run(() => renderer.RenderAllDrawings(docx, ct), ct);
        } catch (OperationCanceledException) {
            return Results.Json(new { error = "render_cancelled" }, statusCode: 504);
        } catch (Exception ex) {
            log.LogError(ex, "[{Rid}] render failed", requestId);
            return Results.Json(new { error = "render_failed" }, statusCode: 500);
        }
        sw.Stop();

        if (drawings.Count == 0) {
            log.LogInformation(
                "[{Rid}] no_drawings host={Host} bytes={Bytes} render_ms={Ms}",
                requestId, uri.Host, docx.Length, sw.ElapsedMilliseconds);
            return Results.Json(new { error = "no_drawings_found" }, statusCode: 422);
        }

        var encoded = drawings.Select(b => b == null ? null : Convert.ToBase64String(b)).ToArray();
        log.LogInformation(
            "[{Rid}] ok host={Host} bytes={Bytes} drawings={Count} render_ms={Ms}",
            requestId, uri.Host, docx.Length, drawings.Count, sw.ElapsedMilliseconds);
        return Results.Json(new { drawings = encoded });
    }

    private static readonly JsonSerializerOptions JsonOpts = new() {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static string AssemblyVersion() {
        var asm = typeof(Program).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return info ?? "unknown";
    }

    private static string AssemblyBuiltAt() {
        try {
            string path = typeof(Program).Assembly.Location;
            return string.IsNullOrEmpty(path) ? "unknown"
                : File.GetLastWriteTimeUtc(path).ToString("o");
        } catch { return "unknown"; }
    }

    public sealed class RenderRequest {
        public string? DocxUrl { get; set; }
    }

    // Marker type so the logger category reads cleanly in journal output.
    public sealed class Program_LoggerCategory { }
}

public sealed class Config {
    public string SofficePath { get; init; } = "/usr/bin/soffice";
    public int MaxConcurrency { get; init; } = Environment.ProcessorCount;
    public int RenderTimeoutMs { get; init; } = 120_000;
    public int DownloadTimeoutMs { get; init; } = 30_000;
    public int MaxDocxBytes { get; init; } = 100 * 1024 * 1024;
    public string AllowedDownloadHostPatterns { get; init; } =
        @"^([a-z0-9.-]+\.)?s3([.-][a-z0-9-]+)*\.amazonaws\.com$";

    public static Config FromEnvironment() => new() {
        SofficePath = Environment.GetEnvironmentVariable("SOFFICE_PATH") ?? "/usr/bin/soffice",
        MaxConcurrency = ParseInt("MAX_CONCURRENCY", Environment.ProcessorCount),
        RenderTimeoutMs = ParseInt("RENDER_TIMEOUT_MS", 120_000),
        DownloadTimeoutMs = ParseInt("DOWNLOAD_TIMEOUT_MS", 30_000),
        MaxDocxBytes = ParseInt("MAX_DOCX_BYTES", 100 * 1024 * 1024),
        AllowedDownloadHostPatterns = Environment.GetEnvironmentVariable("ALLOWED_DOWNLOAD_HOST_PATTERNS")
            ?? @"^([a-z0-9.-]+\.)?s3([.-][a-z0-9-]+)*\.amazonaws\.com$",
    };

    private static int ParseInt(string name, int fallback) {
        string? raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out int n) && n > 0 ? n : fallback;
    }
}

public sealed class HostAllowlist {
    private readonly Regex pattern;

    public HostAllowlist(string regex) {
        this.pattern = new Regex(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    public bool IsAllowed(string host) =>
        !string.IsNullOrEmpty(host) && pattern.IsMatch(host);
}

public sealed class SofficeProbe {
    private readonly Config config;
    private readonly ILogger<SofficeProbe> log;
    private bool healthy;

    public SofficeProbe(Config config, ILogger<SofficeProbe> log) {
        this.config = config;
        this.log = log;
    }

    public bool IsHealthy => healthy;

    public void Refresh() {
        healthy = Probe();
    }

    private bool Probe() {
        if (!File.Exists(config.SofficePath)) {
            log.LogWarning("soffice not found at {Path}", config.SofficePath);
            return false;
        }
        try {
            var psi = new ProcessStartInfo {
                FileName = config.SofficePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--version");
            using var p = Process.Start(psi);
            if (p is null) return false;
            if (!p.WaitForExit(5_000)) {
                try { p.Kill(entireProcessTree: true); } catch { /* best-effort */ }
                return false;
            }
            return p.ExitCode == 0;
        } catch (Exception ex) {
            log.LogWarning(ex, "soffice probe failed");
            return false;
        }
    }
}
