
using System.IO;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments {

class Logging {

    // https://github.com/serilog/serilog-extensions-logging-file#appsettingsjson-configuration

    internal static ILoggerFactory Factory = LoggerFactory.Create(builder => { builder.SetMinimumLevel(LogLevel.None); });

    internal static void SetConsoleAndFile(LogLevel level = LogLevel.Debug) {
        Factory = LoggerFactory.Create(builder => { builder
            .AddConsole()
            .AddFile("logs/judgments.log")
            .SetMinimumLevel(level);
        });
    }

    internal static void SetConsole(LogLevel level) {
        Factory = LoggerFactory.Create(builder => { builder
            .AddConsole()
            .SetMinimumLevel(level);
        });
    }

    internal static void SetFile(FileInfo file, LogLevel level = LogLevel.Information) {
        if (file is null)
            Factory = LoggerFactory.Create(builder => { builder.SetMinimumLevel(LogLevel.None); });
        else
            Factory = LoggerFactory.Create(builder => { builder
                .AddFile(file.FullName)
                .SetMinimumLevel(level);
        });
    }

}

}
