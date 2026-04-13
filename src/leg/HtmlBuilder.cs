
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;

namespace UK.Gov.Legislation {

/// <summary>
/// Renders parsed AKN documents to HTML5 by running them through src/leg/akn2html.xsl.
/// The stylesheet is XSLT 2.0, which the .NET BCL can't run (its XslCompiledTransform
/// is XSLT 1.0 only). Rather than take a heavyweight Saxon NuGet dependency, this class
/// shells out to the Saxon JAR that ships with Oxygen XML Editor — the same engine the
/// repo's existing manual workflow already uses.
///
/// Set <c>OXYGEN_HOME</c> to override the default Oxygen install location; on Windows
/// the default is "C:\Program Files\Oxygen XML Editor 27".
/// </summary>
public static class HtmlBuilder {

    private const string DefaultOxygenHomeWindows = @"C:\Program Files\Oxygen XML Editor 27";
    private const string SaxonMainClass = "net.sf.saxon.Transform";

    /// <summary>
    /// Render the given parsed AKN document as an HTML5 string.
    /// </summary>
    /// <param name="akn">The parsed AKN XML document.</param>
    /// <param name="imageBase">Value for the stylesheet's <c>image-base</c> parameter.
    /// Empty string is the right default for local review where the HTML file sits next
    /// to the SaveImages output. The XSLT default is "/" (absolute paths).</param>
    public static string Build(XmlDocument akn, string imageBase = "") {
        string oxygenHome = ResolveOxygenHome();
        string javaExe = Path.Combine(oxygenHome, "jre", "bin", "java.exe");
        string libGlob = Path.Combine(oxygenHome, "lib", "*");

        if (!File.Exists(javaExe))
            throw new InvalidOperationException(
                $"Could not find Oxygen-bundled Java at '{javaExe}'. Set the OXYGEN_HOME " +
                $"environment variable to your Oxygen XML Editor install directory.");

        string tempDir = Path.Combine(Path.GetTempPath(), "leg-html-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string aknPath = Path.Combine(tempDir, "in.akn");
        string xslPath = Path.Combine(tempDir, "akn2html.xsl");
        string htmlPath = Path.Combine(tempDir, "out.html");

        try {
            akn.Save(aknPath);
            ExtractEmbeddedXsl(xslPath);

            var psi = new ProcessStartInfo {
                FileName = javaExe,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-cp");
            psi.ArgumentList.Add(libGlob);
            psi.ArgumentList.Add(SaxonMainClass);
            psi.ArgumentList.Add($"-xsl:{xslPath}");
            psi.ArgumentList.Add($"-s:{aknPath}");
            psi.ArgumentList.Add($"-o:{htmlPath}");
            psi.ArgumentList.Add($"image-base={imageBase}");

            using var process = Process.Start(psi);
            process.WaitForExit();
            if (process.ExitCode != 0) {
                string stderr = process.StandardError.ReadToEnd();
                string stdout = process.StandardOutput.ReadToEnd();
                throw new InvalidOperationException(
                    $"Saxon exited with code {process.ExitCode}.\nstderr:\n{stderr}\nstdout:\n{stdout}");
            }

            return File.ReadAllText(htmlPath);
        } finally {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    private static string ResolveOxygenHome() {
        string fromEnv = Environment.GetEnvironmentVariable("OXYGEN_HOME");
        return !string.IsNullOrEmpty(fromEnv) ? fromEnv : DefaultOxygenHomeWindows;
    }

    private static void ExtractEmbeddedXsl(string destination) {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream("leg.akn2html.xsl");
        if (stream == null)
            throw new InvalidOperationException("Embedded resource 'leg.akn2html.xsl' not found.");
        using var fileStream = File.Create(destination);
        stream.CopyTo(fileStream);
    }

}

}
