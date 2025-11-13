#nullable enable

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

using Amazon.S3;
using Amazon.S3.Model;

using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

using Moq;
namespace test.backlog;

public class MockS3Client : Mock<IAmazonS3>, IDisposable
{
    public const string TestBucket = "test-bucket";
    public byte[]? CapturedContent { get; private set; }
    public string? CapturedKey { get; private set; }

    public MockS3Client()
    {
        Environment.SetEnvironmentVariable("BUCKET_NAME", TestBucket);

        Setup(x => x.PutObjectAsync(
                It.Is<PutObjectRequest>(req =>
                    req.BucketName == TestBucket &&
                    req.ContentType == "application/gzip"),
                It.IsAny<CancellationToken>())
            )
            .Callback<PutObjectRequest, CancellationToken>((req, token) =>
            {
                // Capture the key (UUID) and content
                CapturedKey = req.Key;
                using var ms = new MemoryStream();
                req.InputStream.CopyTo(ms);
                CapturedContent = ms.ToArray();
            })
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });
    }
    
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("BULK_NUMBERS_PATH", null);
    }

    public string GetFileFromCapturedContent(string fileExtension)
    {
        if (CapturedContent is null)
            throw new InvalidOperationException("No content was captured");

        using var gzipStream = new GZipInputStream(new MemoryStream(CapturedContent));
        using var archive = new TarInputStream(gzipStream, Encoding.UTF8);

        var entry = archive.GetNextEntry();
        while (entry != null && !entry.Name.EndsWith(fileExtension)) {
            entry = archive.GetNextEntry();
        }

        if (entry is null)
            throw new FileNotFoundException();

        using var reader = new StreamReader(archive);

        return reader.ReadToEnd();
    }
}
