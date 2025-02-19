
using System;
using System.IO;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

namespace Backlog.Src
{

    class Bucket
    {

        private static readonly Amazon.RegionEndpoint London = Amazon.RegionEndpoint.EUWest2;

        private static readonly string BucketName =  Environment.GetEnvironmentVariable("BUCKET_NAME");

        private static readonly string AccessKeyId =  Environment.GetEnvironmentVariable("ACCESS_KEY_ID");

        private static readonly string SecretAccessKey =  Environment.GetEnvironmentVariable("SECRET_ACCESS_KEY");

        private static readonly AmazonS3Client Client = new(AccessKeyId, SecretAccessKey, London);

        internal static Task<PutObjectResponse> UploadBundle(string key, byte[] bundle)
        {
            PutObjectRequest request = new()
            {
                BucketName = BucketName,
                Key = key,
                ContentType = "application/gzip",
                InputStream = new MemoryStream(bundle)
            };
            return Client.PutObjectAsync(request);
        }
    }

}
