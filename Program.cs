
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

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
            xml = System.Console.OpenStandardOutput();
        else
            xml = output.OpenWrite();
        Transform(docx, xml, log, test);
        xml.Close();
        docx.Close();
    }

    private static void Transform(Stream docx, Stream xml, bool log, bool test) {
        Func<Stream, ILazyBundle> parser = UK.Gov.Legislation.Judgments.AkomaNtoso.Parser.ParseCourtOfAppealJudgment;
        ILazyBundle bundle = parser(docx);
        Serializer.Serialize(bundle.Judgment, xml);
        if (test) {
            Tester.Result result = Tester.Test(bundle.Judgment);
            if (!log) Tester.Print(result);
        }
        bundle.Close();
    }

}
