using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;

using Microsoft.Extensions.Logging;

using Logging = UK.Gov.Legislation.Judgments.Logging;

namespace UK.Gov.Legislation.Common.Rendering {

// IDrawingRenderer that POSTs DOCX bytes to a remote RenderService over HTTP.
// Same wire contract as DocxToImageRenderer running in-process: returns
// rendered drawings in document order.
//
// Failure semantics match LocalSubprocessRenderer: on any transport or
// upstream failure we log a warning and return an empty array. Strict-mode
// behaviour ("fail the whole document if a drawing can't be rendered") is
// enforced one layer up by the parser's allowUnrenderedCharts flag, not here.
public sealed class HttpRenderer : IDrawingRenderer {

    private static readonly ILogger logger =
        Logging.Factory.CreateLogger<HttpRenderer>();

    private readonly HttpClient http;
    private readonly Uri renderEndpoint;
    private readonly bool ownsClient;

    public HttpRenderer(string endpoint, TimeSpan? timeout = null, HttpClient httpClient = null) {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentNullException(nameof(endpoint));
        // Append /render-bytes to the supplied base (e.g. http://10.10.x.x:8080).
        var baseUri = new Uri(endpoint, UriKind.Absolute);
        this.renderEndpoint = new Uri(baseUri, "/render-bytes");
        if (httpClient is null) {
            this.http = new HttpClient { Timeout = timeout ?? TimeSpan.FromMinutes(3) };
            this.ownsClient = true;
        } else {
            this.http = httpClient;
            this.ownsClient = false;
        }
    }

    public IReadOnlyList<byte[]> RenderAllDrawings(byte[] docx, CancellationToken ct) {
        if (docx == null || docx.Length == 0) return Array.Empty<byte[]>();

        try {
            using var content = new ByteArrayContent(docx);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            // IDrawingRenderer is sync; the parse pipeline is sync. Awaiting via
            // GetAwaiter().GetResult() is acceptable here because the call is the
            // unit of long-running work, not a fan-out from a UI thread.
            using var resp = http.PostAsync(renderEndpoint, content, ct).GetAwaiter().GetResult();

            if (!resp.IsSuccessStatusCode) {
                logger.LogWarning(
                    "HttpRenderer: {Status} from {Endpoint}",
                    (int) resp.StatusCode, renderEndpoint);
                return Array.Empty<byte[]>();
            }

            string json = resp.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("drawings", out var drawings)
                    || drawings.ValueKind != JsonValueKind.Array) {
                logger.LogWarning("HttpRenderer: response had no 'drawings' array");
                return Array.Empty<byte[]>();
            }

            var result = new byte[drawings.GetArrayLength()][];
            int i = 0;
            foreach (var el in drawings.EnumerateArray()) {
                result[i++] = el.ValueKind == JsonValueKind.Null
                    ? null
                    : Convert.FromBase64String(el.GetString() ?? string.Empty);
            }
            return result;
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            logger.LogWarning(ex, "HttpRenderer call to {Endpoint} failed", renderEndpoint);
            return Array.Empty<byte[]>();
        }
    }

    public void Dispose() {
        if (ownsClient) http.Dispose();
    }

}

}
