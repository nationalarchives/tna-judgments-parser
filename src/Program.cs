
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

using System.CommandLine;
using System.CommandLine.Invocation;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using Api = UK.Gov.NationalArchives.Judgments.Api;

class Program {

    static RootCommand command;

    static Program() {
        command = new RootCommand {
            new Option<FileInfo>("--input", description: "the .docx file") { ArgumentHelpName = "file",  IsRequired = true },
            new Option<FileInfo>("--output", description: "the .xml file") { ArgumentHelpName = "file" },
            new Option<FileInfo>("--output-zip", description: "the .zip file") { ArgumentHelpName = "file" },
            new Option<FileInfo>("--log", description: "the log file") { ArgumentHelpName = "file" },
            new Option<bool>("--test", description: "whether to test the result")
        };
        command.Handler = CommandHandler.Create<FileInfo, FileInfo, FileInfo, FileInfo, bool>(Transform);
    }

    static int Main(string[] args) {
        return command.InvokeAsync(args).Result;
    }

    static void Transform(FileInfo input, FileInfo output, FileInfo outputZip, FileInfo log, bool test) {
        if (log is not null) {
            Logging.SetFile(log, LogLevel.Debug);
            ILogger logger = Logging.Factory.CreateLogger<Program>();
            logger.LogInformation("parsing " + input.FullName);
        }
        byte[] docx = File.ReadAllBytes(input.FullName);
        Api.Request request = new Api.Request { Content = docx };
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
            using (var zip = entry.Open())
                zip.Write(image.Content, 0, image.Content.Length);
        }
    }

    private static void Print(Api.Meta meta) {
        Console.WriteLine($"The judgment's uri is { meta.Uri }");
        Console.WriteLine($"The court is { meta.Court }");
        Console.WriteLine($"The case citation is { meta.Cite }");
        Console.WriteLine($"The judgment date is { meta.Date }");
        Console.WriteLine($"The case name is { meta.Name }");
    }

}
