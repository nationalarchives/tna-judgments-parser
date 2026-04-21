# Chart / SmartArt rendering — implementation plan

Companion to `docs/chart-rendering-spike.md`. The spike confirmed Pattern B
(small LibreOffice rendering service called by a pure-managed parser) is
the right architecture. This document describes how we build it.

## Goals

- Parser stays pure-managed; no LibreOffice in Lambda or the EC2 batch
  runner.
- The same parser produces the same AKN output in live (Lambda) and
  historic (EC2) pipelines.
- When a drawing can't be rendered (service down, unsupported content,
  bad input), **fail loudly by default** so the caller knows the AKN
  lost content. An explicit opt-in flag switches to the text placeholder
  behaviour.
- Other TNA services (judgments parser, backlog tools, editorial
  tooling) can call the same rendering endpoint.

## Strict vs lenient failure handling

When a drawing has no embedded bitmap (charts, SmartArt, some shapes):

```
Drawing without blip
   │
   ├─► attempt render (LibreOffice service)
   │      │
   │      ├─► success ─────────► embed PNG, continue
   │      │
   │      └─► failure (timeout, unsupported, service error)
   │              │
   │              ├─ strict (default) ─────► throw UnrenderableDrawingException
   │              │
   │              └─ lenient (opt-in) ─────► emit [Chart: descr] text placeholder
```

**Strict is the default.** Silent content loss is worse than a loud
failure — CI/automation needs to see when output is incomplete.

**Lenient is opt-in** via:

- CLI: `--allow-unrendered-charts`
- API: `allowUnrenderedCharts` parameter on each `Helper.Parse` (same
  shape as the existing `manifestationName` parameter).

The exception carries enough context to act on:

```text
UnrenderableDrawingException
  DocumentName: ukia_20250012_en.docx
  DrawingIndex: 8 (of 11 drawings)
  GraphicType: c:chart
  Caption:     "This chart shows the composition of total HE sector income..."
  RenderError: {ServiceDown|Timeout|HttpError|Unsupported}
```

### Rollout strategy for the flag

Flipping the default to strict immediately would break every current
caller (no service deployed yet, every chart doc would throw). We
install the flag now with a conservative rollout:

| Phase | When | Parser default | Callers pass flag? |
|---|---|---|---|
| 0 | Now, before service exists | `lenient` (temporary) | — |
| 1 | Flag added, service not yet deployed | `strict` | Every current caller passes `--allow-unrendered-charts` so nothing breaks |
| 2 | Render service deployed and reachable | `strict` | Callers drop the flag; failures are real failures |
| 3 | Optional: some callers keep `--allow-unrendered-charts` for best-effort use cases (e.g. preview generation) | `strict` | Only explicit opt-ins |

Phase 1 ships in this branch. Phases 2–3 happen after service deployment.

## Rendering service

### Shape

Small stateless HTTP service, one endpoint:

```
POST /render
  Request:  multipart/form-data
              docx: <file>            (whole DOCX, required)
              drawing_index: 0-based  (which drawing in document order)
  Response: 200 image/png             (rendered drawing)
            422 JSON                  (unsupported drawing type)
            500 JSON                  (internal error)
```

### Internals

1. Write the incoming DOCX to `/tmp/<uuid>/input.docx`.
2. Invoke `soffice --headless --convert-to pdf --outdir /tmp/<uuid> input.docx`.
3. Parse the resulting PDF with [PdfPig](https://github.com/UglyToad/PdfPig).
4. Walk images in document order; return the one matching `drawing_index`.
   (The correlation between DOCX drawing index and PDF image index is
   deterministic for LibreOffice's Writer → PDF export — verify with
   fixtures.)
5. Clean up the scratch directory.

### Deployment

- **AWS ECS Fargate** service, 1 GB RAM, 0.5 vCPU, desired count ≥ 1
  (always warm). ~$15–30/month.
- Private VPC load balancer; callers reach it over internal networking.
- Container image: Debian slim + `libreoffice-core` + `libreoffice-writer`
  + the small .NET 8 API. ~400 MB image.
- CloudWatch logs structured; alarm on HTTP 5xx rate > 1% over 5 min.
- No persistence; horizontally scalable if we ever need more than one
  task.

### Local dev path

For local development and CI tests, the parser can invoke `soffice`
directly as a subprocess, bypassing the network hop. A config flag
selects the renderer:

```
LegChartRenderer__Mode = "LocalSubprocess"   (dev/test)
LegChartRenderer__Mode = "HttpService"       (prod)
LegChartRenderer__Endpoint = "https://render.tna.internal"
```

Behaviour is identical; only the transport differs.

## Parser integration

### New abstraction

```csharp
namespace UK.Gov.Legislation.Common.Rendering;

public interface IDrawingRenderer {
    byte[] RenderDrawingToPng(byte[] docx, int drawingIndex, CancellationToken ct);
}

public sealed class LocalSubprocessRenderer : IDrawingRenderer { ... }
public sealed class HttpRenderer            : IDrawingRenderer { ... }
public sealed class NullRenderer            : IDrawingRenderer { ... }  // always throws — for tests
```

The renderer is injected into `BaseHelper` alongside the existing
`LegImageProcessor`. CLI and TRE Lambda select the implementation at
startup from config.

### New exception

```csharp
namespace UK.Gov.Legislation.Common.Rendering;

public sealed class UnrenderableDrawingException : Exception {
    public string DocumentName { get; }
    public int DrawingIndex { get; }
    public string GraphicType { get; }
    public string Caption { get; }
    public string RenderError { get; }
    ...
}
```

### Parser flow change

In `src/parsers/common/Inline.cs`, `MapRunChild` for a `Drawing` today:

```csharp
if (e is Drawing draw) {
    IInline img = WImageRef.Make(main, draw);
    return img ?? (IInline) MakeDrawingPlaceholder(draw, rProps);
}
```

After this plan:

```csharp
if (e is Drawing draw) {
    IInline img = WImageRef.Make(main, draw);
    if (img != null) return img;

    // Missing blip. Try to render; honour the caller's strict/lenient choice.
    byte[] png = _renderer?.TryRender(main, draw, _docxBytes, _drawingIndex);
    if (png != null) return CreateRenderedImageRef(png, draw);

    if (_allowUnrenderedCharts) {
        return MakeDrawingPlaceholder(draw, rProps);
    }
    throw new UnrenderableDrawingException(...);
}
```

Threading `_docxBytes`, `_drawingIndex`, and `_allowUnrenderedCharts`
through the inline pipeline is the non-trivial part — that work is
isolated to `Inline.cs`, `BaseHelper.cs`, and a parser-wide context
object.

### API surface

Each `Helper.Parse(...)` gains an optional parameter:

```csharp
public static IXmlDocument Parse(
    byte[] docx,
    string filename,
    bool simplify = true,
    string manifestationName = Builder.DefaultManifestationName,
    bool allowUnrenderedCharts = false);
```

Same parameter on `BaseHelper.Parse`. CLI wires the flag.

## Reusability

The HTTP service is a generic "render OOXML drawings to images" primitive:

- **Judgments parser** hits the same case; can adopt the same renderer
  abstraction.
- **Backlog tools** can call the HTTP endpoint during archival processing.
- **Editorial / QA** tooling can preview docx drawings without installing
  anything locally.
- **Any language** can call the HTTP endpoint — Python, Node, curl — the
  contract is HTTP, not a .NET library.

## Work breakdown

On this branch:

1. Remove ScottPlot spike artefacts.
2. Add `IDrawingRenderer` abstraction and the `LocalSubprocessRenderer`
   (invokes `soffice` directly). Defer the HTTP client until the service
   exists.
3. Add `UnrenderableDrawingException`.
4. Add `allowUnrenderedCharts` parameter from `Helper.Parse` down to
   `Inline.MapRunChild` via a context object.
5. Wire the parser: on blip-less drawing, try render → if null, strict
   vs lenient decision.
6. CLI flag `--allow-unrendered-charts`.
7. Unit tests:
   - Renderer returns PNG for a known chart → parser embeds it.
   - Renderer returns null → strict mode throws; lenient mode emits
     placeholder.
8. Regenerate leg fixtures with the renderer attached; compare to spike
   output to verify fidelity.

In a follow-up ticket (not this branch):

- Build the Fargate service.
- Add `HttpRenderer`.
- Flip parser defaults per phase 2 of the rollout.
- Add retries, circuit breaker, timeout policy to the HTTP client.
- Produce a "render failures" report from batch runs to prioritise any
  remaining long tail (e.g. `chartEx` features LibreOffice doesn't cover).
