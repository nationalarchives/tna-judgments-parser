#nullable enable

using Microsoft.Extensions.DependencyInjection;

using System;

using Xunit;
using Microsoft.Extensions.Time.Testing;
using UK.Gov.Legislation.Judgments.Parse;
using test.backlog.EndToEndTests;

namespace test.backlog;

public class TestProgram(ITestOutputHelper testOutputHelper) : BaseEndToEndTests(testOutputHelper)
{
    [Fact]
    public void ConfigureDependencyInjection_HasAccurateTimeProviderByDefault()
    {
        // Ensure that we're not running with dependency injections
        Backlog.Src.Program.DependencyInjectionOverrides.Clear();
        
        ServiceProvider serviceProvider = Backlog.Src.Program.ConfigureDependencyInjection("pathToDataFolder", "trackerPath", "judgmentsFilePath", "hmctsFilePath", "bucketName");
        
        DateTimeOffset timeProviderTime = serviceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        
        Assert.IsNotType<FakeTimeProvider>(serviceProvider.GetRequiredService<TimeProvider>());
        Assert.Equal(timeProviderTime, DateTime.UtcNow, TimeSpan.FromSeconds(1));

        
    }
}
