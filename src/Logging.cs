
using System.IO;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments {

class Logging {

    // https://github.com/serilog/serilog-extensions-logging-file#appsettingsjson-configuration

    internal static ILoggerFactory Factory = LoggerFactory.Create(builder => { builder.SetMinimumLevel(LogLevel.None); });

    internal static void SetConsoleAndFile() {
        Factory = Factory = LoggerFactory.Create(builder => { builder
            .AddConsole()
            .AddFile("logs/judgments.log")
            .SetMinimumLevel(LogLevel.Debug);
        });
    }

    internal static void SetConsole() {
        Factory = Factory = LoggerFactory.Create(builder => { builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information);
        });
    }

    internal static void SetFile(FileInfo file) {
        if (file is null)
            Factory = Factory = LoggerFactory.Create(builder => { builder.SetMinimumLevel(LogLevel.None); });
        else
            Factory = Factory = LoggerFactory.Create(builder => { builder
                .AddFile(file.FullName)
                .SetMinimumLevel(LogLevel.Information);
        });
    }

}

}
