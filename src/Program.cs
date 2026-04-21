using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

using System.CommandLine;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation;
using UK.Gov.Legislation.Judgments;
using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;
using Api = UK.Gov.NationalArchives.Judgments.Api;
using EM = UK.Gov.Legislation.ExplanatoryMemoranda;
using IA = UK.Gov.Legislation.ImpactAssessments;
using EN = UK.Gov.Legislation.ExplanatoryNotes;
using TN = UK.Gov.Legislation.TranspositionNotes;
using CoP = UK.Gov.Legislation.CodesOfPractice;
using OD = UK.Gov.Legislation.OtherDocuments;
using UK.Gov.Legislation.Lawmaker;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("test")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("backlog")]

public class Program {
    private const int Success = 0;

    private static readonly RootCommand Command;
    private static readonly Option<FileInfo> InputOption = new("--input"){ Description = "the .docx file", Required = true };
    private static readonly Option<FileInfo> OutputOption = new("--output"){ Description = "the .xml file"};
    private static readonly Option<FileInfo> OutputZipOption = new("--output-zip"){ Description = "the .zip file"};
    private static readonly Option<FileInfo> OutputHtmlOption = new("--output-html"){ Description = "the .html file (leg doc types only)"};
    private static readonly Option<FileInfo> LogOption = new("--log"){ Description = "the log file"};
    private static readonly Option<bool> TestOption = new("--test"){ Description = "whether to test the result"};
    private static readonly Option<FileInfo> AttachmentOption = new("--attachment"){ Description = "an associated file to include"};
    private static readonly Option<string> HintOption = new("--hint"){ Description = "the type of document: 'em', 'en', 'ia', 'tn', 'cop', 'od' or a Lawmaker type such as 'nipubb', 'uksi', or 'ukprib'"};
    private static readonly Option<string> SubtypeOption = new("--subtype"){ Description = "the subtype of the document e.g. 'order'. Only applicable if --hint is a secondary type"};
    private static readonly Option<string> ProcedureOption = new("--procedure"){ Description = "the procedure of the document e.g. 'made', 'draftaffirm'. Only applicable if --hint is a secondary type"};
    private static readonly Option<string[]> LanguageOption = new("--language"){ Description = "the language(s) of the document - the default is English", AllowMultipleArgumentsPerToken = true};
    private static readonly Option<string> ManifestationNameOption = new("--manifestation-name"){ Description = "value for FRBRManifestation/FRBRdate/@name identifying the workflow (default: historic-akn-transform)"};

    static Program() {
        Command = new RootCommand {
            InputOption,
            OutputOption,
            OutputZipOption,
            OutputHtmlOption,
            LogOption,
            TestOption,
            AttachmentOption,
            HintOption,
            SubtypeOption,
            ProcedureOption,
            LanguageOption,
            ManifestationNameOption,
        };
        Command.SetAction(Transform);
    }

    public static int Main(string[] args) {
        return Command.Parse(args).Invoke();
    }

    private static (FileInfo input, FileInfo output, FileInfo outputZip, FileInfo outputHtml, FileInfo log, bool test, FileInfo attachment,
        string hint, string subType, string procedure, string[] language, string manifestationName) GetParsedArgs(ParseResult parseResult)
    {
        return (input: parseResult.GetValue(InputOption),
            output: parseResult.GetValue(OutputOption),
            outputZip: parseResult.GetValue(OutputZipOption),
            outputHtml: parseResult.GetValue(OutputHtmlOption),
            log: parseResult.GetValue(LogOption),
            test: parseResult.GetValue(TestOption),
            attachment: parseResult.GetValue(AttachmentOption),
            hint: parseResult.GetValue(HintOption),
            subType: parseResult.GetValue(SubtypeOption),
            procedure: parseResult.GetValue(ProcedureOption),
            language: parseResult.GetValue(LanguageOption),
            manifestationName: parseResult.GetValue(ManifestationNameOption) ?? UK.Gov.Legislation.Builder.DefaultManifestationName);
    }

    static int Transform(ParseResult parseResult)
    {
        var (input, output, outputZip, outputHtml, log, test, attachment, hint, subType, procedure, language, manifestationName) = GetParsedArgs(parseResult);

        ILogger logger = null;
        if (log is not null) {
            Logging.SetConsoleAndFile(log, LogLevel.Debug);
            logger = Logging.Factory.CreateLogger<Program>();
            logger.LogInformation("parsing " + input.FullName);
        }
        if ("em".Equals(hint, StringComparison.InvariantCultureIgnoreCase)) {
            TransformEM(input, output, outputZip, outputHtml, log, test, attachment, manifestationName);
            return Success;
        }

        if ("ia".Equals(hint, StringComparison.InvariantCultureIgnoreCase)) {
            TransformIA(input, output, outputZip, outputHtml, log, test, attachment, manifestationName);
            return Success;
        }

        if ("en".Equals(hint, StringComparison.InvariantCultureIgnoreCase)) {
            TransformEN(input, output, outputZip, outputHtml, log, test, attachment, manifestationName);
            return Success;
        }

        if ("tn".Equals(hint, StringComparison.InvariantCultureIgnoreCase)) {
            TransformTN(input, output, outputZip, outputHtml, log, test, attachment, manifestationName);
            return Success;
        }

        if ("cop".Equals(hint, StringComparison.InvariantCultureIgnoreCase)) {
            TransformCoP(input, output, outputZip, outputHtml, log, test, attachment, manifestationName);
            return Success;
        }

        if ("od".Equals(hint, StringComparison.InvariantCultureIgnoreCase)) {
            TransformOD(input, output, outputZip, outputHtml, log, test, attachment, manifestationName);
            return Success;
        }

        DocName? docName = DocNames.GetDocName(hint);
        if (docName != null) {
            LegislationClassifier classifier = new LegislationClassifier((DocName)docName, subType, procedure);
            LanguageService languageService = new LanguageService(language);
            var xml = Helper.LocalParse(input.FullName, classifier, languageService).Xml;
            if (output is not null)
                File.WriteAllText(output.FullName, xml);
            else
                Console.WriteLine(xml);
            return Success;
        } else {
            logger?.LogCritical("unrecognized document type: {}", hint);
            Console.Error.WriteLine($"Error: Invalid hint '{hint}'. Supported values: 'em', 'en', 'ia', 'tn', 'cop', 'od', or a Lawmaker type such as 'nipubb', 'uksi', or 'ukprib'.");
            Environment.Exit(1);
        }
        byte[] docx = File.ReadAllBytes(input.FullName);
        Api.Request request;
        if (attachment is null) {
            request = new Api.Request { Content = docx };
        } else {
            byte[] docxA = File.ReadAllBytes(attachment.FullName);
            Api.Attachment a = new Api.Attachment { Content = docxA, Filename = attachment.Name };
            request = new Api.Request { Content = docx, Attachments = new Api.Attachment[] { a } };
        }

        var parser = new Api.Parser(Logging.Factory.CreateLogger<Api.Parser>(), new AkN.Validator());
        Api.Response response = parser.Parse(request);
        if (outputZip is not null)
            SaveZip(response, outputZip);
        else if (output is not null)
            File.WriteAllText(output.FullName, response.Xml);
        else
            Console.WriteLine(response.Xml);
        if (test)
            Print(response.Meta);

        return Success;
    }

    static void TransformEM(FileInfo input, FileInfo output, FileInfo outputZip, FileInfo outputHtml, FileInfo log, bool test, FileInfo attachment, string manifestationName)
        => RunLegTransform(EM.Helper.Parse, input, output, outputZip, outputHtml, log, attachment, manifestationName);

    static void TransformIA(FileInfo input, FileInfo output, FileInfo outputZip, FileInfo outputHtml, FileInfo log, bool test, FileInfo attachment, string manifestationName)
        => RunLegTransform(IA.Helper.Parse, input, output, outputZip, outputHtml, log, attachment, manifestationName);

    static void TransformEN(FileInfo input, FileInfo output, FileInfo outputZip, FileInfo outputHtml, FileInfo log, bool test, FileInfo attachment, string manifestationName)
        => RunLegTransform(EN.Helper.Parse, input, output, outputZip, outputHtml, log, attachment, manifestationName);

    static void TransformTN(FileInfo input, FileInfo output, FileInfo outputZip, FileInfo outputHtml, FileInfo log, bool test, FileInfo attachment, string manifestationName)
        => RunLegTransform(TN.Helper.Parse, input, output, outputZip, outputHtml, log, attachment, manifestationName);

    static void TransformCoP(FileInfo input, FileInfo output, FileInfo outputZip, FileInfo outputHtml, FileInfo log, bool test, FileInfo attachment, string manifestationName)
        => RunLegTransform(CoP.Helper.Parse, input, output, outputZip, outputHtml, log, attachment, manifestationName);

    static void TransformOD(FileInfo input, FileInfo output, FileInfo outputZip, FileInfo outputHtml, FileInfo log, bool test, FileInfo attachment, string manifestationName)
        => RunLegTransform(OD.Helper.Parse, input, output, outputZip, outputHtml, log, attachment, manifestationName);

    private delegate IXmlDocument LegParser(
        byte[] docx, string filename, bool simplify,
        string manifestationName, bool allowUnrenderedCharts,
        UK.Gov.Legislation.Common.Rendering.IDrawingRenderer renderer);

    private static void RunLegTransform(LegParser parse, FileInfo input, FileInfo output, FileInfo outputZip, FileInfo outputHtml, FileInfo log, FileInfo attachment, string manifestationName) {
        if (attachment is not null)
            throw new Exception();
        if (log is not null)
            Logging.SetConsoleAndFile(log, LogLevel.Debug);

        byte[] docx = File.ReadAllBytes(input.FullName);
        var parsed = parse(docx, input.Name, true, manifestationName, true, null);

        // Save images alongside whichever file output is requested. Prefer --output's
        // directory; fall back to --output-html's. Both go to the same place if both
        // are set in the same directory (SaveImages overwrites idempotently).
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

    private static void SaveZip(Api.Response response, FileInfo file) {
        using var stream = new FileStream(file.FullName, FileMode.Create);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("judgment.xml");
        using (var zip = entry.Open()) {
            byte[] bytes = Encoding.UTF8.GetBytes(response.Xml);
            zip.Write(bytes, 0, bytes.Length);
        }
        entry = archive.CreateEntry("meta.json");
        using (var zip = entry.Open()) {
            JsonSerializerOptions options = new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            string json = JsonSerializer.Serialize(response.Meta, options);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            zip.Write(bytes, 0, bytes.Length);
        }
        foreach (var image in response.Images) {
            entry = archive.CreateEntry(image.Name);
            using var zip = entry.Open();
            zip.Write(image.Content, 0, image.Content.Length);
        }
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

    private static void Print(Api.Meta meta) {
        Console.Error.WriteLine(meta.Uri);
        Console.Error.WriteLine(meta.Court);
        Console.Error.WriteLine(meta.Date);
        Console.Error.WriteLine(meta.Cite);
        Console.Error.Write(meta.Name);
    }
}
