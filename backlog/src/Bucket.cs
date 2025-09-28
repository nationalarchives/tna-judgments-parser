
using System;
using System.IO;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

namespace Backlog.Src
{

    public    class Bucket
    {
        private static IAmazonS3 client = new AmazonS3Client();
        private static string bucketName = Environment.GetEnvironmentVariable("BUCKET_NAME");

        // For testing purposes
        public static void Configure(IAmazonS3 s3Client, string testBucketName = null)
        {
            client = s3Client;
            if (testBucketName != null)
                bucketName = testBucketName;
        }

        // Reset to default configuration
        public static void ResetConfiguration()
        {
            client = new AmazonS3Client();
            bucketName = Environment.GetEnvironmentVariable("BUCKET_NAME");
        }

        public static Task<PutObjectResponse> UploadBundle(string key, byte[] bundle)
        {
            PutObjectRequest request = new()
            {
                BucketName = bucketName,
                Key = key,
                ContentType = "application/gzip",
                InputStream = new MemoryStream(bundle)
            };
            return client.PutObjectAsync(request);
        }
    }

}
