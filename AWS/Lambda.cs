
using System.Collections.Generic;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UK.Gov.NationalArchives.Judgments.Api {

public class Lambda {

    public APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest gateway) {
        Request request = Request.FromJson(gateway.Body);
        Response response = Parser.Parse(request);
        return OK(response);
    }

    private APIGatewayProxyResponse OK(Response response) {
        return new APIGatewayProxyResponse() {
            StatusCode = 200,
            Headers = new Dictionary<string, string>(1) { { "Content-Type", "application/json" } },
            Body = response.ToJson()
        };
    }

}

}
