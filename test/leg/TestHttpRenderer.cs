using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using UK.Gov.Legislation.Common.Rendering;

namespace UK.Gov.Legislation.Test;

public class TestHttpRenderer {

    [Fact]
    public void RenderAllDrawings_ReturnsEmpty_WhenDocxIsEmpty() {
        var http = new HttpClient(new StubHandler(_ => null));   // never called
        var r = new HttpRenderer("http://example.invalid:8080", httpClient: http);
        var result = r.RenderAllDrawings(Array.Empty<byte>(), CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public void RenderAllDrawings_DecodesBase64DrawingsFromHappyPath() {
        byte[] png1 = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
        byte[] png2 = new byte[] { 0x89, 0x50, 0x4E, 0x47, 4, 5, 6 };
        string body = "{\"drawings\":[\"" + Convert.ToBase64String(png1) + "\",\""
                     + Convert.ToBase64String(png2) + "\"]}";
        var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }));
        var r = new HttpRenderer("http://example.invalid:8080", httpClient: http);

        var result = r.RenderAllDrawings(new byte[] { 0x50, 0x4B }, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(png1, result[0]);
        Assert.Equal(png2, result[1]);
    }

    [Fact]
    public void RenderAllDrawings_ReturnsEmpty_OnHttpError() {
        var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError) {
                Content = new StringContent("{\"error\":\"render_failed\"}")
            }));
        var r = new HttpRenderer("http://example.invalid:8080", httpClient: http);
        var result = r.RenderAllDrawings(new byte[] { 1, 2, 3 }, CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public void RenderAllDrawings_ReturnsEmpty_On422NoDrawings() {
        var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage((HttpStatusCode) 422) {
                Content = new StringContent("{\"error\":\"no_drawings_found\"}")
            }));
        var r = new HttpRenderer("http://example.invalid:8080", httpClient: http);
        var result = r.RenderAllDrawings(new byte[] { 1, 2, 3 }, CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public void RenderAllDrawings_ReturnsEmpty_OnTransportFailure() {
        var http = new HttpClient(new StubHandler(_ => throw new HttpRequestException("connection refused")));
        var r = new HttpRenderer("http://example.invalid:8080", httpClient: http);
        var result = r.RenderAllDrawings(new byte[] { 1, 2, 3 }, CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public void RenderAllDrawings_HandlesNullDrawingsInResponse() {
        // Render service may return nulls in slots where the source had a drawing
        // but rendering that specific one failed. Honour that mapping.
        string body = "{\"drawings\":[null,\"" + Convert.ToBase64String(new byte[] { 1, 2, 3 }) + "\"]}";
        var http = new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }));
        var r = new HttpRenderer("http://example.invalid:8080", httpClient: http);

        var result = r.RenderAllDrawings(new byte[] { 1 }, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Null(result[0]);
        Assert.Equal(new byte[] { 1, 2, 3 }, result[1]);
    }

    [Fact]
    public void RenderAllDrawings_PostsToRenderBytesPath() {
        Uri capturedUri = null;
        string capturedContentType = null;
        var http = new HttpClient(new StubHandler(req => {
            capturedUri = req.RequestUri;
            capturedContentType = req.Content?.Headers.ContentType?.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"drawings\":[]}", Encoding.UTF8, "application/json")
            };
        }));
        var r = new HttpRenderer("http://example.invalid:8080", httpClient: http);

        r.RenderAllDrawings(new byte[] { 1, 2, 3 }, CancellationToken.None);

        Assert.NotNull(capturedUri);
        Assert.Equal("/render-bytes", capturedUri.AbsolutePath);
        Assert.Equal("application/octet-stream", capturedContentType);
    }

    private sealed class StubHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) {
            this.respond = respond;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            var resp = respond(request);
            return resp == null
                ? Task.FromException<HttpResponseMessage>(new InvalidOperationException("stub got an unexpected request"))
                : Task.FromResult(resp);
        }
    }
}
