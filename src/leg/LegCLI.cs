using System;
using System.IO;
using System.IO.Compression;
using System.Text;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using EM = UK.Gov.Legislation.ExplanatoryMemoranda;
using IA = UK.Gov.Legislation.ImpactAssessments;
using EN = UK.Gov.Legislation.ExplanatoryNotes;
using TN = UK.Gov.Legislation.TranspositionNotes;
using CoP = UK.Gov.Legislation.CodesOfPractice;
using OD = UK.Gov.Legislation.OtherDocuments;

namespace UK.Gov.Legislation {

/// <summary>
/// Entry point the shared CLI delegates to for leg-doc-type processing.
/// Keeps Program.cs free of leg-specific dispatch, options handling, and
/// the document.xml ZIP overload.
/// </summary>
public static class LegCLI {

    public static bool IsLegHint(string hint) {
        if (string.IsNullOrEmpty(hint)) return false;
        return hint.Equals("em", StringComparison.InvariantCultureIgnoreCase)
            || hint.Equals("ia", StringComparison.InvariantCultureIgnoreCase)
            || hint.Equals("en", StringComparison.InvariantCultureIgnoreCase)
            || hint.Equals("tn", StringComparison.InvariantCultureIgnoreCase)
            || hint.Equals("cop", StringComparison.InvariantCultureIgnoreCase)
            || hint.Equals("od", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    /// Caller is expected to have checked <see cref="IsLegHint"/> first;
    /// passing a non-leg hint throws.
    /// </summary>
    public static void Transform(
            string hint,
            FileInfo input, FileInfo output, FileInfo outputZip, FileInfo outputHtml,
            FileInfo log, FileInfo attachment, string manifestationName) {
        LegParser parse = SelectParser(hint)
            ?? throw new ArgumentException($"not a leg hint: {hint}");
        RunLegTransform(parse, input, output, outputZip, outputHtml, log, attachment, manifestationName);
    }

    private static LegParser SelectParser(string hint) {
        return hint.ToLowerInvariant() switch {
            "em" => EM.Helper.Parse,
            "ia" => IA.Helper.Parse,
            "en" => EN.Helper.Parse,
            "tn" => TN.Helper.Parse,
            "cop" => CoP.Helper.Parse,
            "od" => OD.Helper.Parse,
            _ => null,
        };
    }

    private delegate IXmlDocument LegParser(
        byte[] docx, string filename, bool simplify,
        string manifestationName, bool allowUnrenderedCharts,
        UK.Gov.Legislation.Common.Rendering.IDrawingRenderer renderer);

    private static void RunLegTransform(
            LegParser parse,
            FileInfo input, FileInfo output, FileInfo outputZip, FileInfo outputHtml,
            FileInfo log, FileInfo attachment, string manifestationName) {
        if (attachment is not null)
            throw new Exception();
        if (log is not null)
            Logging.SetConsoleAndFile(log, LogLevel.Debug);

        byte[] docx = File.ReadAllBytes(input.FullName);
        var renderer = UK.Gov.Legislation.Common.Rendering.RendererFactory.FromEnvironment();
        // Strict mode: LEG_STRICT_RENDER=true makes the parser throw
        // UnrenderableDrawingException when a drawing can't be rendered, which
        // surfaces as a per-document failure in the parse-akn batch job.
        // Default (false) keeps the historic lenient behaviour: emit a
        // placeholder for unrenderable drawings and continue producing AKN.
        bool allowUnrendered = !string.Equals(
            Environment.GetEnvironmentVariable("LEG_STRICT_RENDER"),
            "true", StringComparison.OrdinalIgnoreCase);
        var parsed = parse(docx, input.Name, true, manifestationName, allowUnrendered, renderer);

        string imageDir = output is not null
            ? Path.GetDirectoryName(output.FullName)
            : (outputHtml is not null ? Path.GetDirectoryName(outputHtml.FullName) : null);
        if (!string.IsNullOrEmpty(imageDir))
            parsed.SaveImages(imageDir);

        if (outputHtml is not null)
            File.WriteAllText(outputHtml.FullName, HtmlBuilder.Build(parsed.Document));

        if (outputZip is not null)
            SaveZip(parsed, outputZip);
        else if (output is not null)
            File.WriteAllText(output.FullName, parsed.Serialize());
        else if (outputHtml is null)
            Console.WriteLine(parsed.Serialize());
    }

    private static void SaveZip(IXmlDocument document, FileInfo file) {
        using var stream = new FileStream(file.FullName, FileMode.Create);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("document.xml");
        using (var zip = entry.Open()) {
            byte[] bytes = Encoding.UTF8.GetBytes(document.Serialize());
            zip.Write(bytes, 0, bytes.Length);
        }
        foreach (var image in document.Images) {
            entry = archive.CreateEntry(image.Name);
            using var zip = entry.Open();
            byte[] imageBytes = image.Read();
            zip.Write(imageBytes, 0, imageBytes.Length);
        }
    }

}

}
