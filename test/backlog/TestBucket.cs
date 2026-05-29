#nullable enable

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Backlog;

using Microsoft.Extensions.Logging;

using Moq;

using test.backlog.EndToEndTests;
using test.Mocks;

using Xunit;

namespace test.backlog;

public class TestBucket(ITestOutputHelper testOutputHelper)
    : BaseEndToEndTests(testOutputHelper)
{
    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable("IS_TEST", "true");
        base.Dispose(disposing);
    }

    [Fact]
    public void DryRunBucket_WhenNotInDryRunMode_ThrowsArgumentException()
    {
        var options = BacklogParserOptionsHelper.Create(isDryRun: false);

        Assert.Throws<ArgumentException>(() => new DryRunBucket(options, Mock.Of<ILogger<DryRunBucket>>()));
    }

    [Fact]
    public void DryRunBucket_InDryRunMode_CanBeCreated()
    {
        var options = BacklogParserOptionsHelper.Create(isDryRun: true);

        var bucket = new DryRunBucket(options, Mock.Of<ILogger<DryRunBucket>>());

        Assert.NotNull(bucket);
    }

    [Fact]
    public async Task DryRunBucket_UploadBundleAsync_Logs()
    {
        var options = BacklogParserOptionsHelper.Create(isDryRun: true);
        var mockLogger = new MockLogger<DryRunBucket>();
        var bucket = new DryRunBucket(options, mockLogger.Object);

        await bucket.UploadBundleAsync("test-key.tar.gz", [1, 2, 3]);

        mockLogger.VerifyLog("This is a dry run - not uploading to S3", LogLevel.Information);
    }

    [Fact]
    public async Task Bucket_UploadBundleAsync_PutsObjectInS3()
    {
        var options = BacklogParserOptionsHelper.Create(bucketName: "my-bucket");
        var mockLogger = new MockLogger<Bucket>();
        var mockAmazonS3 = new Mock<IAmazonS3>();
        mockAmazonS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK });
        
        var bucket = new Bucket(mockAmazonS3.Object, options, mockLogger.Object);

        await bucket.UploadBundleAsync("test-key.tar.gz", [1, 2, 3]);

        mockLogger.VerifyLog("Uploading test-key.tar.gz to S3", LogLevel.Information);
        mockAmazonS3.Verify(x => x.PutObjectAsync(
            It.Is<PutObjectRequest>(putObjectRequest => putObjectRequest.BucketName == "my-bucket"
                                                        && putObjectRequest.Key == "test-key.tar.gz"
                                                        && putObjectRequest.ContentType == "application/gzip"
                                                        && putObjectRequest.InputStream.Length == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Bucket_UploadBundleAsync_WhenS3UploadFails_ThrowsProblemUploadingFileToS3Exception()
    {
        var options = BacklogParserOptionsHelper.Create(bucketName: "my-bucket");
        var mockLogger = new MockLogger<Bucket>();
        var mockAmazonS3 = new Mock<IAmazonS3>();
        mockAmazonS3.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new PutObjectResponse { HttpStatusCode = HttpStatusCode.InternalServerError });

        var bucket = new Bucket(mockAmazonS3.Object, options, mockLogger.Object);

        await Assert.ThrowsAsync<ProblemUploadingFileToS3Exception>(() =>
            bucket.UploadBundleAsync("test-key.tar.gz", [1, 2, 3])
        );
    }
}
