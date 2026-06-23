using Xunit;

using UK.Gov.Legislation.Common;

namespace UK.Gov.Legislation.ImpactAssessments.Test {

/// <summary>
/// Unit tests for IA filename parsing and short-URI construction across the UK and
/// Scottish jurisdictions. The Scottish cases (ssifia/sdsifia) were previously
/// unsupported: ParseFilename only matched ukia_YYYYNNNN, leaving them with an empty
/// ShortUriComponent (and, when they carried images, a crash in SaveImages).
/// </summary>
public class TestIaUriScheme {

    [Theory]
    // UK ukia and Scottish SI use an 8-digit YYYYNNNN identity.
    [InlineData("ukia_20220111_en", "ukia", 2022, "111", 1, true)]
    [InlineData("ssifia_20130005_en", "ssifia", 2013, "5", 1, true)]
    // Scottish Draft SI is identified by an ISBN, which carries no own year/number.
    [InlineData("sdsifia_9780111019269_en_001", "sdsifia", null, "9780111019269", 1, false)]
    // A trailing _NNN version suffix is stripped and surfaced separately.
    [InlineData("ssifia_20160257_en_002", "ssifia", 2016, "257", 2, true)]
    public void ParsesFilenameSeriesAndIdentity(string filename, string series, int? year, string number, int version, bool hasYearNumber) {
        var id = IALegislationMapping.ParseIAFilename(filename);
        Assert.NotNull(id);
        Assert.Equal(series, id.Series);
        Assert.Equal(year, id.Year);
        Assert.Equal(number, id.Number);
        Assert.Equal(version, id.Version);
        Assert.Equal(hasYearNumber, id.HasYearNumber);
    }

    [Theory]
    // UK ukia keeps its independent series identity in the /impacts path.
    [InlineData("http://www.legislation.gov.uk/id/ukpga/2023/56", "2022", "111", "ukpga/2023/56/impacts/2022/111")]
    // A Scottish SI IA shares its parent SI's year/number.
    [InlineData("http://www.legislation.gov.uk/id/ssi/2013/5", "2013", "5", "ssi/2013/5/impacts/2013/5")]
    // A Scottish Draft SI IA is identified by the draft's ISBN.
    [InlineData("http://www.legislation.gov.uk/id/sdsi/2013/9780111019269", "2013", "9780111019269", "sdsi/2013/9780111019269/impacts/2013/9780111019269")]
    public void BuildsShortUriFromLegislationAndImpactsIdentity(string legUri, string impactsYear, string impactsNumber, string expected) {
        var record = new IAMappingRecord { LegislationUri = legUri, ImpactsYear = impactsYear, ImpactsNumber = impactsNumber };
        Assert.Equal(expected, IALegislationMapping.BuildShortUriComponent(record));
    }

    [Theory]
    // End-to-end against the embedded CSV using stable historical documents. The
    // sdsifia case also exercises the version-suffix fallback: the DOCX is
    // ..._en_001 while the CSV key is ..._en.
    [InlineData("ssifia_20130005_en", "ssi/2013/5/impacts/2013/5")]
    [InlineData("sdsifia_9780111019269_en_001", "sdsi/2013/9780111019269/impacts/2013/9780111019269")]
    public void ResolvesScottishIaFromCsv(string filename, string expectedShortUri) {
        var record = IALegislationMapping.GetMappingRecord(filename);
        Assert.NotNull(record);
        Assert.Equal(expectedShortUri, IALegislationMapping.BuildShortUriComponent(record));
    }

}

}
