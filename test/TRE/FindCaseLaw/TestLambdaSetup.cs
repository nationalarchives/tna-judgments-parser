using System;
using System.IO;
using System.Text;

using Shouldly;

using UK.Gov.NationalArchives.CaseLaw.TRE;

using Xunit;

namespace test.TRE.FindCaseLaw;

public class TestLambdaSetup
{
    [Fact]
    public void FunctionHandlerResolvesWorkerFromAppHost()
    {
        var originalRegion = Environment.GetEnvironmentVariable("AWS_REGION");
        Environment.SetEnvironmentVariable("AWS_REGION", "eu-west-2");
        try
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("""{"parser-inputs": null}"""));

            using var output = new Lambda().FunctionHandler(input);

            new StreamReader(output).ReadToEnd().ShouldContain("parser-inputs is null");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AWS_REGION", originalRegion);
        }
    }
}
