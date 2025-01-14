using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

namespace Backlog.Src
{

    class Bucket
    {

        private static readonly Amazon.RegionEndpoint London = Amazon.RegionEndpoint.EUWest2;

        private static readonly AmazonS3Client Client = new(Secrets.AccessKeyId, Secrets.SecretAccessKey, London);

        internal static Task<PutObjectResponse> UploadBundle(string key, byte[] bundle)
        {
            PutObjectRequest request = new()
            {
                BucketName = Secrets.BucketName,
                Key = key,
                ContentType = "application/gzip",
                InputStream = new MemoryStream(bundle)
            };
            return Client.PutObjectAsync(request);
        }
    }

}
