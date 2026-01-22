using System.Text.Json;

namespace NationalArchives.FindCaseLaw.Utils.Tests;

public class CourtStoreTests
{
    private readonly CourtStore courtStore = new();

    [Theory]
    [InlineData("UKSC")]
    [InlineData("uksc")]
    [InlineData("EWHC-Chancery-Appeals")]
    [InlineData("EWHC-CHANCERY-APPEALS")]
    [InlineData("SIAC")]
    [InlineData("UKFTT-Estate")]
    public void Exists_GivenARealCourtCode_ReturnsTrue(string courtCode)
    {
        var result = courtStore.Exists(courtCode);
        
        Assert.True(result);
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("123")]
    [InlineData("TRIB")]
    [InlineData("not a court code")]
    public void Exists_GivenABadCourtCode_ReturnsFalse(string courtCode)
    {
        var result = courtStore.Exists(courtCode);
        
        Assert.False(result);
    }
}
