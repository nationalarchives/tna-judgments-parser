#nullable enable

using System;

using Microsoft.Extensions.Logging;

using Moq;

namespace test.Mocks;

public class MockLogger<T> : Mock<ILogger<T>>
{
    public MockLogger<T> VerifyLog(string expectedMessage, LogLevel expectedLogLevel = LogLevel.Debug, Times? times = null)
    {
        times ??= Times.Once();

        Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == expectedLogLevel),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == expectedMessage),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)), (Times)times);

        return this;
    }
}
