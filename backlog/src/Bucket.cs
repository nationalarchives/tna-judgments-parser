#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Backlog.Options;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backlog;

public interface IBucket
{
    Task UploadBundleAsync(string key, byte[] bundle);
}

public class DryRunBucket : IBucket
{
    private readonly ILogger<DryRunBucket> logger;

    public DryRunBucket(IOptions<BacklogParserOptions> backlogParserOptions, ILogger<DryRunBucket> logger)
    {
        if (backlogParserOptions.Value.IsDryRun)
        {
            this.logger = logger;
        }
        else
        {
            throw new ArgumentException("Dry run bucket should only be used in dry run mode",
                nameof(backlogParserOptions));
        }
    }

    public Task UploadBundleAsync(string key, byte[] bundle)
    {
        logger.LogInformation("This is a dry run - not uploading to S3");
        return Task.CompletedTask;
    }
}

public class Bucket(IAmazonS3 client, IOptions<BacklogParserOptions> backlogParserOptions, ILogger<Bucket> logger)
    : IBucket
{
    public async Task UploadBundleAsync(string key, byte[] bundle)
    {
        logger.LogInformation("Uploading {BundleFileName} to S3", key);
        PutObjectRequest request = new()
        {
            BucketName = backlogParserOptions.Value.BucketName,
            Key = key,
            ContentType = "application/gzip",
            InputStream = new MemoryStream(bundle)
        };
        var response = await client.PutObjectAsync(request);

        if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            throw new ProblemUploadingFileToS3Exception($"Failed to upload {key} to S3");
    }
}
