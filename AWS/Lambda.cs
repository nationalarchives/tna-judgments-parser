
using System;
using System.Collections.Generic;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UK.Gov.NationalArchives.Judgments.Api {

public class Lambda {

    public APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest gateway) {
        Request request;
        try {
            request = Request.FromJson(gateway.Body);
        } catch (Exception e) {
            return Error(400, e);
        }
        if (request.Content is null)
            return Error(400, "'content' cannot be null");
        Response response;
        try {
            response = Parser.Parse(request);
        } catch (Exception e) {
            return Error(500, e);
        }
        return OK(response);
    }

    private APIGatewayProxyResponse OK(Response response) {
        return new APIGatewayProxyResponse() {
            StatusCode = 200,
            Headers = new Dictionary<string, string>(1) { { "Content-Type", "application/json" } },
            Body = response.ToJson()
        };
    }

    private APIGatewayProxyResponse Error(int status, string message) {
        Dictionary<string, object> response = new() {
            { "status", status },
            { "message", message }
        };
        return new APIGatewayProxyResponse() {
            StatusCode = status,
            Headers = new Dictionary<string, string>(1) { { "Content-Type", "application/json" } },
            Body = JsonSerializer.Serialize(response, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
        };
    }

    private APIGatewayProxyResponse Error(int status, Exception e) {
        Dictionary<string, object> response = new() {
            { "status", status },
            { "error", e.GetType().Name },
            { "message", e.Message },
            // { "stack", e.StackTrace }
        };
        return new APIGatewayProxyResponse() {
            StatusCode = status,
            Headers = new Dictionary<string, string>(1) { { "Content-Type", "application/json" } },
            Body = JsonSerializer.Serialize(response, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
        };
    }

}

}