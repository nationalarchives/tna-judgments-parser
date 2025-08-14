
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

using System.CommandLine;
using System.CommandLine.NamingConventionBinder;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation;
using UK.Gov.Legislation.Judgments;
using Api = UK.Gov.NationalArchives.Judgments.Api;
using EM = UK.Gov.Legislation.ExplanatoryMemoranda;
using UK.Gov.Legislation.Lawmaker;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("test")]

class Program {

    static RootCommand command;

    static Program() {
        command = new RootCommand {
            new Option<FileInfo>("--input", description: "the .docx file") { ArgumentHelpName = "file",  IsRequired = true },
            new Option<FileInfo>("--output", description: "the .xml file") { ArgumentHelpName = "file" },
            new Option<FileInfo>("--output-zip", description: "the .zip file") { ArgumentHelpName = "file" },
            new Option<FileInfo>("--log", description: "the log file") { ArgumentHelpName = "file" },
            new Option<bool>("--test", description: "whether to test the result"),
            new Option<FileInfo>("--attachment", description: "an associated file to include") { ArgumentHelpName = "file" },
            new Option<string>("--hint", description: "the type of document: 'em' or a Lawmaker type such as 'nipubb', 'uksi', or 'ukprib'"),
            new Option<string>("--subtype", description: "the subtype of document e.g. 'order'"),
            new Option<string>("--procedure", description: "only applicable --hint is a secondary type - the subtype of document e.g. 'order'"),
        };
        command.Handler = CommandHandler.Create<
            FileInfo,
            FileInfo,
            FileInfo,
            FileInfo,
            bool,
            FileInfo,
            string,
            string,
            string
        >(Transform);
    }

    static int Main(string[] args) {
        return command.InvokeAsync(args).Result;
    }

    static void Transform(
        FileInfo input,
        FileInfo output,
        FileInfo outputZip,
        FileInfo log,
        bool test,
        FileInfo attachment,
        string hint,
        string subType,
        string procedure
    ) {
        ILogger logger = null;
        if (log is not null) {
            Logging.SetConsoleAndFile(log, LogLevel.Debug);
            logger = Logging.Factory.CreateLogger<Program>();
            logger.LogInformation("parsing " + input.FullName);
        }
        if ("em".Equals(hint, StringComparison.InvariantCultureIgnoreCase)) {
            TransformEM(input, output, outputZip, log, test, attachment);
            return;
        }
        DocName? docName = DocNames.GetDocName(hint);
        if (docName != null) {
            var xml = UK.Gov.Legislation.Lawmaker.Helper.LocalParse(input.FullName, new LegislationClassifier((DocName)docName, subType, procedure)).Xml;
            if (output is not null)
                File.WriteAllText(output.FullName, xml);
            else
                Console.WriteLine(xml);
            return;
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
        Api.Response response = Api.Parser.Parse(request);
        if (outputZip is not null)
            SaveZip(response, outputZip);
        else if (output is not null)
            File.WriteAllText(output.FullName, response.Xml);
        else
            Console.WriteLine(response.Xml);
        if (test)
            Print(response.Meta);
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
