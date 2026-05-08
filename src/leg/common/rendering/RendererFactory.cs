using System;

using Microsoft.Extensions.Logging;

using Logging = UK.Gov.Legislation.Judgments.Logging;

namespace UK.Gov.Legislation.Common.Rendering {

// Picks an IDrawingRenderer based on environment variables.  Used by the CLI
// and any batch wrappers that want a single switch between local and remote
// rendering without hard-coding the choice.
//
//   LEG_RENDER_MODE       local  | http  | null   (default: local on dev, null on Lambda/EC2 with no SOFFICE_PATH)
//   LEG_RENDER_ENDPOINT   http base URL when MODE=http (e.g. http://10.10.x.x:8080)
//   SOFFICE_PATH          path to soffice when MODE=local
//                         (default: /usr/bin/soffice on Linux, the standard install path on Windows)
public static class RendererFactory {

    private static readonly ILogger logger =
        Logging.Factory.CreateLogger("UK.Gov.Legislation.Common.Rendering.RendererFactory");

    public static IDrawingRenderer FromEnvironment() {
        string mode = (Environment.GetEnvironmentVariable("LEG_RENDER_MODE") ?? "").Trim().ToLowerInvariant();
        switch (mode) {
            case "http": return BuildHttp();
            case "null": return new NullRenderer();
            case "local":
            case "":
                return BuildLocalOrNull();
            default:
                logger.LogWarning("LEG_RENDER_MODE='{Mode}' not recognised; falling back to NullRenderer", mode);
                return new NullRenderer();
        }
    }

    private static IDrawingRenderer BuildHttp() {
        string endpoint = Environment.GetEnvironmentVariable("LEG_RENDER_ENDPOINT");
        if (string.IsNullOrWhiteSpace(endpoint)) {
            logger.LogWarning("LEG_RENDER_MODE=http but LEG_RENDER_ENDPOINT not set; using NullRenderer");
            return new NullRenderer();
        }
        logger.LogInformation("renderer=http endpoint={Endpoint}", endpoint);
        return new HttpRenderer(endpoint);
    }

    private static IDrawingRenderer BuildLocalOrNull() {
        string sofficePath = Environment.GetEnvironmentVariable("SOFFICE_PATH");
        if (string.IsNullOrWhiteSpace(sofficePath)) {
            sofficePath = OperatingSystem.IsWindows()
                ? @"C:\Program Files\LibreOffice\program\soffice.exe"
                : "/usr/bin/soffice";
        }
        if (!System.IO.File.Exists(sofficePath)) {
            logger.LogInformation("renderer=null (soffice not installed at {Path})", sofficePath);
            return new NullRenderer();
        }
        logger.LogInformation("renderer=local soffice={Path}", sofficePath);
        return new LocalSubprocessRenderer(sofficePath);
    }

}

}
