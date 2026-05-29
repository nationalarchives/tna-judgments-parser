using System;
using System.IO;

using UK.Gov.Legislation.Common.Rendering;

namespace UK.Gov.Legislation.Test {

internal static class LocalRendererHelper {

    // Rendering is opt-in: tests render only when LEG_SOFFICE_PATH points at a real
    // LibreOffice. Unset (the default, and on CI) means GetOrNull returns null, so
    // snapshot tests run render-off and are deterministic regardless of what happens
    // to be installed on the machine. No install path is hardcoded.
    private static readonly Lazy<IDrawingRenderer> _renderer = new(() => {
        string path = Environment.GetEnvironmentVariable("LEG_SOFFICE_PATH");
        return !string.IsNullOrEmpty(path) && File.Exists(path)
            ? new LocalSubprocessRenderer(path)
            : null;
    });

    public static IDrawingRenderer GetOrNull() => _renderer.Value;

}

}
