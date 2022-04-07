
using System.IO;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments {

public class Logging {

    // https://github.com/serilog/serilog-extensions-logging-file#appsettingsjson-configuration

    public static ILoggerFactory Factory = LoggerFactory.Create(builder => { builder.SetMinimumLevel(LogLevel.None); });

    public static void SetConsoleAndFile(FileInfo file, LogLevel level = LogLevel.Information) {
        Factory = LoggerFactory.Create(builder => { builder
            .AddConsole()
            .AddFile(file.FullName, level)
            .SetMinimumLevel(level);
        });
    }

    public static void SetConsole(LogLevel level) {
        Factory = LoggerFactory.Create(builder => { builder
            .AddConsole()
            .SetMinimumLevel(level);
        });
    }

    public static void SetFile(FileInfo file, LogLevel level = LogLevel.Information) {
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
