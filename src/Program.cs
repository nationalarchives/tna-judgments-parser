
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using Api = UK.Gov.NationalArchives.Judgments.Api;

class Program {

    static RootCommand command;

    static Program() {
        command = new RootCommand {
            new Option<FileInfo>("--input", description: "the .docx file") { ArgumentHelpName = "file",  IsRequired = true },
            new Option<FileInfo>("--output", description: "the .xml file") { ArgumentHelpName = "file" },
            new Option<FileInfo>("--log", description: "the log file") { ArgumentHelpName = "file" },
            new Option<bool>("--test", description: "whether to test the result")
        };
        command.Handler = CommandHandler.Create<FileInfo, FileInfo, FileInfo, bool>(Transform);
    }

    static int Main(string[] args) {
        return command.InvokeAsync(args).Result;
    }

    static void Transform(FileInfo input, FileInfo output, FileInfo log, bool test) {
        if (log is not null) {
            Logging.SetFile(log);
            ILogger logger = Logging.Factory.CreateLogger<Program>();
            logger.LogInformation("parsing " + input.FullName);
            Transform(input, output, true, test);
        } else {
            Transform(input, output, false, test);
        }
    }

    private static void Transform(FileInfo input, FileInfo output, bool log, bool test) {
        FileStream docx = input.OpenRead();
        Stream xml;
        if (output is null)
            xml = Console.OpenStandardOutput();
        else
            xml = output.OpenWrite();
        Transform(docx, xml, log, test);
        xml.Close();
        docx.Close();
    }

    private static void Transform(Stream docx, Stream xml, bool log, bool test) {
        MemoryStream ms = new MemoryStream();
        docx.CopyTo(ms);
        Api.Request request = new Api.Request() { Content = ms.ToArray() };
        Api.Response response = Api.Parser.Parse(request);
        using StreamWriter writer = new StreamWriter(xml, System.Text.Encoding.UTF8);
        writer.Write(response.Xml);
        writer.Flush();
        if (test)
            Print(response.Meta);
    }

    private static void Print(Api.Meta meta) {
        Console.WriteLine($"The judgment's uri is { meta.Uri }");
        Console.WriteLine($"The court is { meta.Court }");
        Console.WriteLine($"The case citation is { meta.Cite }");
        Console.WriteLine($"The judgment date is { meta.Date }");
        Console.WriteLine($"The case name is { meta.Name }");
    }

}
