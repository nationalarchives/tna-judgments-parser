
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using Amazon.Lambda;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;

// [assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UK.Gov.NationalArchives.Judgments.Api {

public class LambdaAsync {

    private static readonly string AccessKeyId;
    private static readonly string SecretAccessKey;
    public static readonly Amazon.RegionEndpoint Region;
    public static readonly string AsyncFunctionName;
    public static readonly string Bucket;

    static LambdaAsync() {
        IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
        AccessKeyId = config["AWS:AccessKeyId"];
        SecretAccessKey = config["AWS:SecretAccessKey"];
        try {
            string regionEndpoint = config["RegionEndpoint"];
            Region = Amazon.RegionEndpoint.GetBySystemName(regionEndpoint);
        } catch (Exception) {
        }
        AsyncFunctionName = config["AWS:AsyncFunctionName"];
        Bucket = config["AWS:Bucket"];
    }

    public APIGatewayProxyResponse Post(APIGatewayProxyRequest gateway) {
        Request request;
        try {
            request = Request.FromJson(gateway.Body);
        } catch (Exception e) {
            return Lambda.Error(400, e);
        }
        if (request.Content is null)
            return Lambda.Error(400, "'content' cannot be null");
        string guid = Guid.NewGuid().ToString();
        InvokeResponse response = QueueParse(guid, request);
        if (response.StatusCode != 202)
            return Lambda.Error(500, "error queuing function");
        APIGatewayProxyResponse accepted = Accepted(guid);
        SaveApiResponse(guid, accepted);
        return accepted;
    }

    private InvokeResponse QueueParse(string guid, Request request) {
        AmazonLambdaClient lambda = new AmazonLambdaClient(AccessKeyId, SecretAccessKey, Region);
        RequestWithGuid request2 = RequestWithGuid.Make(guid, request);
        string payload = JsonSerializer.Serialize(request2);
        InvokeRequest request3 = new InvokeRequest() {
            FunctionName = AsyncFunctionName,
            Payload = payload,
            InvocationType = InvocationType.Event
        };
        Task<InvokeResponse> task = lambda.InvokeAsync(request3);
        return task.Result;
    }

    public void DoParse(RequestWithGuid request) {
        try {
            Response response = Parser.Parse(request);
            SaveOK(request.Guid, response);
        } catch (Exception e) {
            SaveError(request.Guid, 500, e);
        }
    }

    private APIGatewayProxyResponse Accepted(string guid) {
        Dictionary<string, object> response = new() {
            { "token", guid }
        };
        return new APIGatewayProxyResponse() {
            StatusCode = 202,
            Headers = new Dictionary<string, string>(1) { { "Content-Type", "application/json" } },
            Body = JsonSerializer.Serialize(response)
        };
    }

    /* S3 */

    // private static APIGatewayProxyResponse GetApiResponse(string guid) {
    //     AmazonS3Client s3 = new AmazonS3Client(AccessKeyId, SecretAccessKey, Region);
    //     GetObjectResponse response = s3.GetObjectAsync(Bucket, guid).Result;
    //     using Stream stream = response.ResponseStream;
    //     using MemoryStream ms = new MemoryStream();
    //     return JsonSerializer.Deserialize<APIGatewayProxyResponse>(ms.ToArray());
    // }

    private static void SaveOK(string guid, Response response) {
        APIGatewayProxyResponse gateway = Lambda.OK(response);
        SaveApiResponse(guid, gateway);
    }

    private static void SaveError(string guid, int status, Exception e) {
        APIGatewayProxyResponse gateway = Lambda.Error(status, e);
        SaveApiResponse(guid, gateway);
    }

    private static PutObjectResponse SaveApiResponse(string guid, APIGatewayProxyResponse response) {
        string json = JsonSerializer.Serialize(response);
        using MemoryStream stream = new MemoryStream();
        using StreamWriter writer = new StreamWriter(stream);
        writer.Write(json);
        writer.Flush();
        PutObjectRequest request = new PutObjectRequest() {
            BucketName = Bucket,
            Key = guid,
            ContentType = "application/json",
            InputStream = stream
        };
        AmazonS3Client s3 = new AmazonS3Client(AccessKeyId, SecretAccessKey, Region);
        return s3.PutObjectAsync(request).Result;
    }

}

public class RequestWithGuid : Request {

    public string Guid { get; set; }

    public RequestWithGuid() { }

    internal static RequestWithGuid Make(string guid, Request request) {
        return new RequestWithGuid() {
            Guid = guid,
            Filename = request.Filename,
            Content = request.Content,
            Hint = request.Hint,
            Meta = request.Meta,
            Attachments = request.Attachments
        };
    }

}

}
