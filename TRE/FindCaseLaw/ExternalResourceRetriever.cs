using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using TRE.FindCaseLaw.Inputs;

using UK.Gov.NationalArchives.Judgments.Api;

namespace TRE.FindCaseLaw;

public interface IExternalResourceRetriever
{
    byte[] GetDocx(ParserInputs inputs);
    (List<Attachment> attachments, List<string> errors) GetAttachments(IEnumerable<string> attachmentUrls);
}

public class ExternalResourceRetriever(IAmazonS3 s3, ILogger<ExternalResourceRetriever> logger)
    : IExternalResourceRetriever
{
    public byte[] GetDocx(ParserInputs inputs)
    {
        if (!string.IsNullOrEmpty(inputs.DocumentUrl))
        {
            using var http = new HttpClient();
            var task = http.GetByteArrayAsync(inputs.DocumentUrl);
            return task.Result;
        }
        else
        {
            var request = new GetObjectRequest { BucketName = inputs.S3InputBucket, Key = inputs.S3InputKey };
            var task = s3.GetObjectAsync(request);
            var response = task.Result;
            using var ms = new MemoryStream();
            response.ResponseStream.CopyTo(ms);
            return ms.ToArray();
        }
    }

    public (List<Attachment> attachments, List<string> errors) GetAttachments(IEnumerable<string> attachmentUrls)
    {
        List<Attachment> attachments = new();
        List<string> errors = new();

        using var http = new HttpClient();
        foreach (var url in attachmentUrls)
        {
            try
            {
                var content = http.GetByteArrayAsync(url).Result;
                attachments.Add(new Attachment { Content = content });
            }
            catch (Exception e)
            {
                logger.LogError(e, "error reading attachment: {}", e.Message);
                errors.Add("error reading attachment");
            }
        }

        return (attachments, errors);
    }
}
