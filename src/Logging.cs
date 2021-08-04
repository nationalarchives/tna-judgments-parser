
using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments {

class Logging {

    internal static ILoggerFactory Factory = LoggerFactory.Create(builder => { builder
        .AddConsole()
        .AddFile("judgments.log")
        .SetMinimumLevel(LogLevel.Debug);
    });

}

}
