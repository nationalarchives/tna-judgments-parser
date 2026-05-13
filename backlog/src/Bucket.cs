#nullable enable

using System.IO;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Backlog.Options;

using Microsoft.Extensions.Options;

namespace Backlog.Src;

public class Bucket(IAmazonS3 client, IOptions<BacklogParserOptions> backlogParserOptions)
{
    public async Task UploadBundleAsync(string key, byte[] bundle)
    {
        PutObjectRequest request = new()
        {
            BucketName = backlogParserOptions.Value.BucketName,
            Key = key,
            ContentType = "application/gzip",
            InputStream = new MemoryStream(bundle)
        };
        await client.PutObjectAsync(request);
    }
}
