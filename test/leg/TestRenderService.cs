using System;

using Xunit;

using UK.Gov.Legislation.RenderService;

namespace UK.Gov.Legislation.Test;

public class TestRenderService {

    [Theory]
    [InlineData("legislation.int.eu-west-2.transfer.s3.eu-west-2.amazonaws.com", true)]
    [InlineData("legislation.int.eu-west-2.transfer.s3.amazonaws.com", true)]
    [InlineData("s3.eu-west-2.amazonaws.com", true)]
    [InlineData("s3-accelerate.amazonaws.com", true)]
    [InlineData("bucket.s3.dualstack.eu-west-2.amazonaws.com", true)]
    [InlineData("evil.example.com", false)]
    [InlineData("amazonaws.com", false)]                          // missing s3
    [InlineData("s3.amazonaws.com.evil.com", false)]              // suffix attack
    [InlineData("", false)]
    public void HostAllowlist_DefaultPattern_AcceptsS3OnlyAndRejectsImpostors(string host, bool expected) {
        var allowlist = new HostAllowlist(new Config().AllowedDownloadHostPatterns);
        Assert.Equal(expected, allowlist.IsAllowed(host));
    }

    [Fact]
    public void HostAllowlist_CustomPattern_OverridesDefault() {
        var allowlist = new HostAllowlist(@"^cdn\.example\.com$");
        Assert.True(allowlist.IsAllowed("cdn.example.com"));
        Assert.False(allowlist.IsAllowed("s3.amazonaws.com"));
    }

    [Fact]
    public void Config_FromEnvironment_UsesDefaultsWhenUnset() {
        // Snapshot then clear so other tests can't bleed in via env.
        ClearAll();
        Config c = Config.FromEnvironment();
        Assert.Equal("/usr/bin/soffice", c.SofficePath);
        Assert.Equal(120_000, c.RenderTimeoutMs);
        Assert.Equal(30_000, c.DownloadTimeoutMs);
        Assert.Equal(100 * 1024 * 1024, c.MaxDocxBytes);
        Assert.True(c.MaxConcurrency >= 1);
    }

    [Fact]
    public void Config_FromEnvironment_RejectsNonPositive() {
        Environment.SetEnvironmentVariable("RENDER_TIMEOUT_MS", "0");
        Environment.SetEnvironmentVariable("MAX_DOCX_BYTES", "-1");
        try {
            Config c = Config.FromEnvironment();
            Assert.Equal(120_000, c.RenderTimeoutMs);                 // fell back
            Assert.Equal(100 * 1024 * 1024, c.MaxDocxBytes);          // fell back
        } finally {
            ClearAll();
        }
    }

    [Fact]
    public void Config_FromEnvironment_HonoursSetValues() {
        Environment.SetEnvironmentVariable("SOFFICE_PATH", "/opt/lo/soffice");
        Environment.SetEnvironmentVariable("MAX_CONCURRENCY", "2");
        Environment.SetEnvironmentVariable("RENDER_TIMEOUT_MS", "60000");
        Environment.SetEnvironmentVariable("ALLOWED_DOWNLOAD_HOST_PATTERNS", @"^cdn\.example\.com$");
        try {
            Config c = Config.FromEnvironment();
            Assert.Equal("/opt/lo/soffice", c.SofficePath);
            Assert.Equal(2, c.MaxConcurrency);
            Assert.Equal(60_000, c.RenderTimeoutMs);
            Assert.Equal(@"^cdn\.example\.com$", c.AllowedDownloadHostPatterns);
        } finally {
            ClearAll();
        }
    }

    private static void ClearAll() {
        foreach (string n in new[] {
            "SOFFICE_PATH", "MAX_CONCURRENCY", "RENDER_TIMEOUT_MS",
            "DOWNLOAD_TIMEOUT_MS", "MAX_DOCX_BYTES", "ALLOWED_DOWNLOAD_HOST_PATTERNS"
        }) {
            Environment.SetEnvironmentVariable(n, null);
        }
    }
}
