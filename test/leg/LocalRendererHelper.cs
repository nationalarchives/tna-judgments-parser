using System;
using System.IO;

using UK.Gov.Legislation.Common.Rendering;

namespace UK.Gov.Legislation.Test {

internal static class LocalRendererHelper {

    private const string WindowsSofficePath = @"C:\Program Files\LibreOffice\program\soffice.exe";
    private const string LinuxSofficePath = "/usr/bin/soffice";

    private static readonly Lazy<IDrawingRenderer> _renderer = new(() => {
        string path = Environment.GetEnvironmentVariable("LEG_SOFFICE_PATH");
        if (string.IsNullOrEmpty(path)) {
            if (File.Exists(WindowsSofficePath)) path = WindowsSofficePath;
            else if (File.Exists(LinuxSofficePath)) path = LinuxSofficePath;
        }
        return !string.IsNullOrEmpty(path) && File.Exists(path)
            ? new LocalSubprocessRenderer(path)
            : null;
    });

    public static IDrawingRenderer GetOrNull() => _renderer.Value;

}

}
