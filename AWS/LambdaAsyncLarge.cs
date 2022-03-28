
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Amazon.Lambda;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Model;
using Amazon.S3;
using Amazon.S3.Model;

using UK.Gov.Legislation.Judgments;

// [assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UK.Gov.NationalArchives.Judgments.Api {

public class LambdaAsyncLarge {

    private static ILogger logger;

    internal static readonly Amazon.RegionEndpoint Region = Amazon.RegionEndpoint.EUWest2;
    private static readonly string AsyncFunctionName = "judgments-parse-async6";

    static LambdaAsyncLarge() {
        logger = Lambda.LoggerFactory.CreateLogger<LambdaAsyncLarge>();
    }

    // public APIGatewayProxyResponse GetSignedUrlForUpload(APIGatewayProxyRequest gateway) {
    //     logger.LogInformation("received request");
    //     string guid = Guid.NewGuid().ToString();
    //     logger.LogInformation($"GUID = { guid }");
    //     GetPreSignedUrlRequest request = new Amazon.S3.Model.GetPreSignedUrlRequest {
    //         BucketName = LambdaAsync.Bucket,
    //         Key = MakeRequestKey(guid),
    //         Verb = HttpVerb.PUT,
    //         ContentType = "application/json",
    //         Expires = DateTime.Now.AddMinutes(5)
    //     };
    //     string url = LambdaAsync.S3.GetPreSignedURL(request);
    //     Dictionary<string, object> response = new() {
    //         { "token", guid }, { "url", url }
    //     };
    //     return new APIGatewayProxyResponse() {
    //         StatusCode = 200,
    //         Headers = new Dictionary<string, string>(1) { { "Content-Type", "application/json" } },
    //         Body = JsonSerializer.Serialize(response)
    //     };
    // }

    /* judgments-parse-async5 */
    public APIGatewayProxyResponse InitiateParseFromS3(APIGatewayProxyRequest gateway) {
        logger.LogInformation("received request");
        string guid;
        try {
            Dictionary<string,string> body = JsonSerializer.Deserialize<Dictionary<string,string>>(gateway.Body);
            guid = body["token"];
        } catch(Exception e) {
            return Lambda.Error(400, e);
        }
        if (string.IsNullOrEmpty(guid))
            return Lambda.Error(400, "guid is empty");
        logger.LogInformation($"GUID = { guid }");
        InvokeResponse response = QueueParse(guid);
        if (response.StatusCode != 202) {
            logger.LogError("error queuing function");
            return Lambda.Error(500, "error queuing function");
        }
        APIGatewayProxyResponse accepted = LambdaAsync.Accepted(guid);
        SaveAccepted(guid, accepted);
        logger.LogInformation("queuing was successful");
        return accepted;
    }

    private static InvokeResponse QueueParse(string guid) {
        AmazonLambdaClient lambda = new AmazonLambdaClient(Region);
        string payload = JsonSerializer.Serialize(guid);
        InvokeRequest request = new InvokeRequest() {
            FunctionName = AsyncFunctionName,
            Payload = payload,
            InvocationType = InvocationType.Event
        };
        Task<InvokeResponse> task = lambda.InvokeAsync(request);
        return task.Result;
    }

    /* judgments-parse-async6 */
    public void DoParse(string guid) {
        logger.LogInformation("received request");
        logger.LogInformation($"GUID = { guid }");
        Request request;
        try {
            request = FetchRequestFromS3(guid);
        } catch (Exception e) {
            SaveError(guid, 500, e);
            return;
        }
        Response response;
        try {
            response = Parser.Parse(request);
        } catch (Exception e) {
            logger.LogError("parse error", e);
            SaveError(guid, 500, e);
            return;
        }
        logger.LogInformation("parse was successful");
        SaveOK(guid, response);
    }

    private static string MakeRequestKey(string guid) {
        return guid + ".request";
    }

    private static Request FetchRequestFromS3(string guid) {
        AmazonS3Client s3 = new AmazonS3Client(Region);
        string key = MakeRequestKey(guid);
        GetObjectResponse response = s3.GetObjectAsync(LambdaAsync.Bucket, key).Result;
        using Stream stream = response.ResponseStream;
        using StreamReader reader = new StreamReader(stream);
        string json = reader.ReadToEnd();
        return Request.FromJson(json);
    }

    private static void SaveAccepted(string guid, APIGatewayProxyResponse accepted) {
        string json = JsonSerializer.Serialize(accepted);
        SaveToS3(guid, json, 202);
    }

    private static void SaveOK(string guid, Response response) {
        string json = response.ToJson();
        SaveToS3(guid, json, 200);
    }

    private static void SaveError(string guid, int status, Exception e) {
        APIGatewayProxyResponse gateway = Lambda.Error(status, e);
        string json = JsonSerializer.Serialize(gateway);
        SaveToS3(guid, json, status);
    }

    private static PutObjectResponse SaveToS3(string guid, string json, int status) {
        using MemoryStream stream = new MemoryStream();
        using StreamWriter writer = new StreamWriter(stream);
        writer.Write(json);
        writer.Flush();
        MetadataCollection metadata = new MetadataCollection();
        PutObjectRequest request = new PutObjectRequest() {
            BucketName = LambdaAsync.Bucket,
            Key = guid,
            ContentType = "application/json",
            InputStream = stream
        };
        request.Metadata.Add("TNA-Status", status.ToString());
        AmazonS3Client s3 = new AmazonS3Client(Region);
        return s3.PutObjectAsync(request).Result;
    }

}

}
