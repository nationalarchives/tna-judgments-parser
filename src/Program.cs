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
    private static readonly Option<bool> ValidateAknOption = new("--validate-akn"){ Description = "instead of transforming, validate --input as an AKN file and emit JSON to stdout" };

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
            ValidateAknOption,
        };
        Command.SetAction(Dispatch);
    }

    static int Dispatch(ParseResult parseResult) {
        if (parseResult.GetValue(ValidateAknOption))
            return ValidateAkn(parseResult);
        return Transform(parseResult);
    }

    static int ValidateAkn(ParseResult parseResult) {
        FileInfo input = parseResult.GetValue(InputOption);
        var xml = new System.Xml.XmlDocument();
        xml.Load(input.FullName);
        var subErrs = Validator.Shared.Validate(xml);
        var aknErrs = Validator.Shared.ValidateAgainstMainAkn(xml);
        var nsmgr = new System.Xml.XmlNamespaceManager(xml.NameTable);
        nsmgr.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
        string expressionUri = (xml.SelectSingleNode("//akn:FRBRExpression/akn:FRBRthis", nsmgr) as System.Xml.XmlElement)?.GetAttribute("value");
        string frbrDateName = (xml.SelectSingleNode("//akn:FRBRExpression/akn:FRBRdate", nsmgr) as System.Xml.XmlElement)?.GetAttribute("name");
        string frbrDateValue = (xml.SelectSingleNode("//akn:FRBRExpression/akn:FRBRdate", nsmgr) as System.Xml.XmlElement)?.GetAttribute("date");
        bool hasPreface = xml.SelectSingleNode("//akn:preface", nsmgr) != null;
        var bodyChildren = xml.SelectNodes("//akn:mainBody/*", nsmgr);
        bool hasBody = bodyChildren != null && bodyChildren.Count > 0;
        var report = new {
            file = input.Name,
            expressionUri,
            frbrDate = new { name = frbrDateName, date = frbrDateValue },
            hasPreface,
            hasBody,
            subschemaErrors = subErrs.ConvertAll(e => new { line = e.Exception?.LineNumber ?? 0, msg = e.Message }),
            mainAknErrors = aknErrs.ConvertAll(e => new { line = e.Exception?.LineNumber ?? 0, msg = e.Message }),
        };
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report));
        return (subErrs.Count == 0 && aknErrs.Count == 0) ? 0 : 1;
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
        if (LegCLI.IsLegHint(hint)) {
            LegCLI.Transform(hint, input, output, outputZip, outputHtml, log, attachment, manifestationName);
            return Success;
        }

        // GetDocName returns null only for empty input and throws for an
        // unrecognised value, so catch that to emit a friendly error rather
        // than an unhandled stack trace.
        if (!string.IsNullOrEmpty(hint)) {
            DocName? docName;
            try {
                docName = DocNames.GetDocName(hint);
            } catch (Exception) {
                docName = null;
            }
            if (docName != null) {
                LegislationClassifier classifier = new LegislationClassifier((DocName)docName, subType, procedure);
                LanguageService languageService = new LanguageService(language);
                var xml = Helper.LocalParse(input.FullName, classifier, languageService).Xml;
                if (output is not null)
                    File.WriteAllText(output.FullName, xml);
                else
                    Console.WriteLine(xml);
                return Success;
            }
            logger?.LogCritical("unrecognized document type: {}", hint);
            Console.Error.WriteLine($"Error: Invalid hint '{hint}'. Supported values: 'em', 'en', 'ia', 'tn', 'cop', 'od', or a Lawmaker type such as 'nipubb', 'uksi', or 'ukprib'.");
            Environment.Exit(1);
        }
        // No hint: fall through to the judgment parser path.
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

    private static void Print(Api.Meta meta) {
        Console.Error.WriteLine(meta.Uri);
        Console.Error.WriteLine(meta.Court);
        Console.Error.WriteLine(meta.Date);
        Console.Error.WriteLine(meta.Cite);
        Console.Error.Write(meta.Name);
    }
}
