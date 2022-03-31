
using System;
using System.Net.Http;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Amazon.Lambda.Core;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.Judgments.Api;

[assembly: LambdaSerializer(typeof(UK.Gov.NationalArchives.CaseLaw.TRE.CamelCaseSerializer))]

namespace UK.Gov.NationalArchives.CaseLaw.TRE {

public class Lambda {

    private static ILogger logger;

    static Lambda() {
        Logging.SetConsole(LogLevel.Debug);
        logger = Logging.Factory.CreateLogger<Lambda>();
    }

    public object FunctionHandler(string url) {
        string json;
        try {
            using HttpClient client = new HttpClient();
            json = client.GetStringAsync(url).Result;
        } catch (Exception e) {
            logger.LogError("error reading request", e);
            return new ErrorResponse { Status = 500, Message = "error reading request" };
        }
        Request request;
        try {
            request = Request.FromJson(json);
        } catch (Exception e) {
            logger.LogError("request is malformed", e);
            return new ErrorResponse { Status = 400, Message = "request is malformed" };
        }
        if (request.Content is null) {
            logger.LogError("content is null");
            return new ErrorResponse { Status = 400, Message = "'content' cannot be null" };
        }
        Response response;
        try {
            response = Parser.Parse(request);
        } catch (Exception e) {
            logger.LogError("parse error", e);
            return new ErrorResponse { Status = 500, Message = "parse error" };
        }
        return response;
    }

}

public class ErrorResponse {

    public bool Error { get; } = true;

    public int Status { get; init; }

    public string Message { get; init; }

}

public class CamelCaseSerializer : Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer {

    public CamelCaseSerializer() : base(options => options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase) { }

}

}
