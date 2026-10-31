using System.IO;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.S3;

using Microsoft.Extensions.Logging;

using TRE.FindCaseLaw;
using TRE.FindCaseLaw.Inputs;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.AkomaNtoso;

using Api = UK.Gov.NationalArchives.Judgments.Api;
using Logging = UK.Gov.Legislation.Judgments.Logging;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace UK.Gov.NationalArchives.CaseLaw.TRE;

public class Lambda
{
    private readonly Worker worker;

    public Lambda()
    {
        var customLoggerProvider = new CustomLoggerProvider();
        Logging.Factory.AddProvider(customLoggerProvider);
        var logger = Logging.Factory.CreateLogger<Worker>();
        var parser = new Api.Parser(Logging.Factory.CreateLogger<Api.Parser>(), new Validator());
        var amazonS3Client = new AmazonS3Client();
        var externalResourceRetriever = new ExternalResourceRetriever(amazonS3Client, Logging.Factory.CreateLogger<ExternalResourceRetriever>());

        worker = new Worker(logger, parser, customLoggerProvider, amazonS3Client, new InputHelper(), externalResourceRetriever);
    }

    public Stream FunctionHandler(Stream input)
    {
        return worker.Process(input);
    }
}
