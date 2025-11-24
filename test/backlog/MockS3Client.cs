#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

using Amazon.S3;
using Amazon.S3.Model;

using Moq;

using Xunit;

namespace test.backlog;

public partial class MockS3Client : Mock<IAmazonS3>, IDisposable
{
    public const string TestBucket = "test-bucket";

    private readonly Dictionary<string, byte[]> s3Captures = new();

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
                using var ms = new MemoryStream();
                req.InputStream.CopyTo(ms);

                s3Captures.Add(req.Key, ms.ToArray());
            })
            .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });
    }

    public IEnumerable<string> CapturedKeys => s3Captures.Keys;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable("BUCKET_NAME", null);
    }

    public void AssertNumberOfUploads(int expectedNumberOfUploads)
    {
        Assert.True(s3Captures.Count == expectedNumberOfUploads,
            $"Expected {expectedNumberOfUploads} upload(s) to S3 but found {s3Captures.Count} upload(s)");
    }

    public void AssertUploadsWereValid()
    {
        foreach (var s3Capture in s3Captures)
        {
            Assert.True(s3Capture.Key is not null, "No key was captured from upload");
            Assert.True(ValidS3KeyRegex().IsMatch(s3Capture.Key), "Key should be a UUID followed by .tar.gz");

            Assert.True(s3Capture.Value is not null, "No content was uploaded to S3");
            Assert.True(s3Capture.Value.Length != 0, "Uploaded content was empty");
        }
    }

    public byte[] GetCapturedContent(string captureKey)
    {
        return s3Captures[captureKey];
    }

    [GeneratedRegex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.tar\.gz$")]
    private static partial Regex ValidS3KeyRegex();
}
