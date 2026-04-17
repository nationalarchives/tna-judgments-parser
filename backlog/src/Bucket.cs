#nullable enable

using System.IO;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

namespace Backlog.Src;

public class Bucket(IAmazonS3 client, string bucketName)
{
    public async Task<PutObjectResponse> UploadBundle(string key, byte[] bundle)
    {
        PutObjectRequest request = new()
        {
            BucketName = bucketName,
            Key = key,
            ContentType = "application/gzip",
            InputStream = new MemoryStream(bundle)
        };
        return await client.PutObjectAsync(request);
    }
}
