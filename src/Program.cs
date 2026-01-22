
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
using UK.Gov.Legislation.Lawmaker;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("test")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("backlog")]

public class Program {
    private const int Success = 0;

    private static readonly RootCommand Command;
    private static readonly Option<FileInfo> InputOption = new("--input"){ Description = "the .docx file", Required = true };
    private static readonly Option<FileInfo> OutputOption = new("--output"){ Description = "the .xml file"};
    private static readonly Option<FileInfo> OutputZipOption = new("--output-zip"){ Description = "the .zip file"};
    private static readonly Option<FileInfo> LogOption = new("--log"){ Description = "the log file"};
    private static readonly Option<bool> TestOption = new("--test"){ Description = "whether to test the result"};
    private static readonly Option<FileInfo> AttachmentOption = new("--attachment"){ Description = "an associated file to include"};
    private static readonly Option<string> HintOption = new("--hint"){ Description = "the type of document: 'em' or a Lawmaker type such as 'nipubb', 'uksi', or 'ukprib'"};
    private static readonly Option<string> SubtypeOption = new("--subtype"){ Description = "the subtype of the document e.g. 'order'. Only applicable if --hint is a secondary type"};
    private static readonly Option<string> ProcedureOption = new("--procedure"){ Description = "the procedure of the document e.g. 'made', 'draftaffirm'. Only applicable if --hint is a secondary type"};
    private static readonly Option<string[]> LanguageOption = new("--language"){ Description = "the language(s) of the document - the default is English", AllowMultipleArgumentsPerToken = true};

    static Program() {
        Command = new RootCommand {
            InputOption,
            OutputOption,
            OutputZipOption,
            LogOption,
            TestOption,
            AttachmentOption,
            HintOption,
            SubtypeOption,
            ProcedureOption,
            LanguageOption,
        };
        Command.SetAction(Transform);
    }

    public static int Main(string[] args) {
        return Command.Parse(args).Invoke();
    }

    private static (FileInfo input, FileInfo output, FileInfo outputZip, FileInfo log, bool test, FileInfo attachment,
        string hint, string subType, string procedure, string[] language) GetParsedArgs(ParseResult parseResult)
    {
        return (input: parseResult.GetValue(InputOption),
            output: parseResult.GetValue(OutputOption),
            outputZip: parseResult.GetValue(OutputZipOption),
            log: parseResult.GetValue(LogOption),
            test: parseResult.GetValue(TestOption),
            attachment: parseResult.GetValue(AttachmentOption),
            hint: parseResult.GetValue(HintOption),
            subType: parseResult.GetValue(SubtypeOption),
            procedure: parseResult.GetValue(ProcedureOption),
            language: parseResult.GetValue(LanguageOption));
    }
    
    static int Transform(ParseResult parseResult)
    {
        var (input, output, outputZip, log, test, attachment, hint, subType, procedure, language) = GetParsedArgs(parseResult);

        ILogger logger = null;
        if (log is not null) {
            Logging.SetConsoleAndFile(log, LogLevel.Debug);
            logger = Logging.Factory.CreateLogger<Program>();
            logger.LogInformation("parsing " + input.FullName);
        }
        if ("em".Equals(hint, StringComparison.InvariantCultureIgnoreCase)) {
            TransformEM(input, output, outputZip, log, test, attachment);
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

    static void TransformEM(FileInfo input, FileInfo output, FileInfo outputZip, FileInfo log, bool test, FileInfo attachment) {
        if (attachment is not null)
            throw new Exception();
        byte[] docx = File.ReadAllBytes(input.FullName);
        var parsed = EM.Helper.Parse(docx);
        if (outputZip is not null)
            SaveZip(parsed, outputZip);
        else if (output is not null)
            File.WriteAllText(output.FullName, parsed.Serialize());
        else
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

    private static void SaveZip(IXmlDocument em, FileInfo file) {
        using var stream = new FileStream(file.FullName, FileMode.Create);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("judgment.xml");
        using (var zip = entry.Open()) {
            byte[] bytes = Encoding.UTF8.GetBytes(em.Serialize());
            zip.Write(bytes, 0, bytes.Length);
        }
        foreach (var image in em.Images) {
            entry = archive.CreateEntry(image.Name);
            using var zip = entry.Open();
            byte[] bytes = image.Read();
            zip.Write(bytes, 0, bytes.Length);
        }
    }

    private static void Print(Api.Meta meta) {
        Console.WriteLine($"The document type is { meta.DocumentType }");
        Console.WriteLine($"The document's uri is { meta.Uri }");
        Console.WriteLine($"The court is { meta.Court }");
        Console.WriteLine($"The case citation is { meta.Cite }");
        Console.WriteLine($"The doc date is { meta.Date }");
        Console.WriteLine($"The doc name is { meta.Name }");
    }

}
