
using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments {

class Logging {

    // https://github.com/serilog/serilog-extensions-logging-file#appsettingsjson-configuration

    internal static ILoggerFactory Factory = LoggerFactory.Create(builder => { builder
        .AddConsole()
        .AddFile("logs/judgments.log")
        .SetMinimumLevel(LogLevel.Debug);
    });

}

}
