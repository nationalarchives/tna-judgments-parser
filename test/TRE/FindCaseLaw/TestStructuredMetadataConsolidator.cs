#nullable enable

using System;
using System.Collections.Generic;

using NationalArchives.FindCaseLaw.Utils.Metadata;
using NationalArchives.FindCaseLaw.Utils.Metadata.Enums;
using NationalArchives.FindCaseLaw.Utils.Metadata.MetadataFieldTypes;

using Shouldly;

using TRE.FindCaseLaw;
using TRE.Metadata.MetadataFieldTypes;

using UK.Gov.Legislation.Judgments;

using Xunit;

using Party = UK.Gov.NationalArchives.CaseLaw.Model.Party;

namespace test.TRE.FindCaseLaw;

public class TestStructuredMetadataConsolidator
{
    private readonly StructuredMetadataConsolidator structuredMetadataConsolidator = new();

    [Fact]
    public void BuildMetadataFields_WithEmptyInputMetadata_ReturnsDocumentMetadata()
    {
        IMetadataField[] documentMetadata =
        [
            new MetadataField<Judge>
            {
                Id = Guid.NewGuid(),
                Name = MetadataFieldName.Judge,
                Value = new Judge("Judge Alpha", "judge-judge-alpha"),
                Source = MetadataSource.Document,
                Timestamp = new DateTime(2026, 11, 03, 13, 23, 10, DateTimeKind.Utc)
            }
        ];

        var result = structuredMetadataConsolidator.BuildMetadataFields([], documentMetadata);

        result.ShouldBeEquivalentTo(documentMetadata);
    }

    [Fact]
    public void BuildMetadataFields_WithEmptyDocumentMetadata_ReturnsInputMetadata()
    {
        IMetadataField[] inputMetadata =
        [
            new MetadataField<Judge>
            {
                Id = Guid.NewGuid(),
                Name = MetadataFieldName.Judge,
                Value = new Judge("Judge Beta", "judge-judge-beta"),
                Source = MetadataSource.Editor,
                Timestamp = new DateTime(2026, 12, 03, 13, 23, 10, DateTimeKind.Utc)
            }
        ];

        var result = structuredMetadataConsolidator.BuildMetadataFields(inputMetadata, []);

        result.ShouldBeEquivalentTo(inputMetadata);
    }

    [Theory]
    [InlineData(MetadataFieldName.CaseNumber)]
    [InlineData(MetadataFieldName.Court)]
    [InlineData(MetadataFieldName.HeadnoteSummary)]
    [InlineData(MetadataFieldName.Jurisdiction)]
    [InlineData(MetadataFieldName.Name)]
    [InlineData(MetadataFieldName.Ncn)]
    [InlineData(MetadataFieldName.SourceFormat)]
    [InlineData(MetadataFieldName.Uri)]
    [InlineData(MetadataFieldName.WebArchivingLink)]
    public void BuildMetadataFields_String_WithDifferentInputAndDocumentMetadataValues_ReturnsBoth(
        MetadataFieldName metadataFieldName)
    {
        var inputMetadata = new MetadataField<string>
        {
            Id = Guid.NewGuid(),
            Name = metadataFieldName,
            Value = "some value",
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 1, 1, 1, 1, 1, DateTimeKind.Utc)
        };

        var documentMetadata = new MetadataField<string>
        {
            Id = Guid.NewGuid(),
            Name = metadataFieldName,
            Value = "other value",
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 2, 2, 2, 2, 2, DateTimeKind.Utc)
        };

        var result = structuredMetadataConsolidator.BuildMetadataFields([inputMetadata], [documentMetadata]);

        result.ShouldBe([inputMetadata, documentMetadata]);
    }

    [Fact]
    public void BuildMetadataFields_Category_WithDifferentInputAndDocumentMetadataValues_ReturnsBoth()
    {
        var inputMetadata = new MetadataField<Category>
        {
            Id = Guid.NewGuid(),
            Name = MetadataFieldName.Category,
            Value = new Category
            {
                Name = "My category",
                Parent = "Bigger category"
            },
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 1, 1, 1, 1, 1, DateTimeKind.Utc)
        };
        var documentMetadata = new MetadataField<Category>
        {
            Id = Guid.NewGuid(),
            Name = MetadataFieldName.Category,
            Value = new Category
            {
                Name = "My second category",
                Parent = "Bigger category"
            },
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 2, 2, 2, 2, 2, DateTimeKind.Utc)
        };

        var result = structuredMetadataConsolidator.BuildMetadataFields([inputMetadata], [documentMetadata]);

        result.ShouldBe([inputMetadata, documentMetadata]);
    }

    [Fact]
    public void BuildMetadataFields_CsvMetadataFileProperties_WithDifferentInputAndDocumentMetadataValues_ReturnsBoth()
    {
        var inputMetadata = new MetadataField<CsvProperties>
        {
            Id = Guid.NewGuid(),
            Name = MetadataFieldName.CsvMetadataFileProperties,
            Value = new CsvProperties("first.csv", "12345abcde", new Dictionary<string, string>
            {
                { "line1", "thing" },
                { "line2", "thing2" }
            }),
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 1, 1, 1, 1, 1, DateTimeKind.Utc)
        };
        var documentMetadata = new MetadataField<CsvProperties>
        {
            Id = Guid.NewGuid(),
            Name = MetadataFieldName.CsvMetadataFileProperties,
            Value = new CsvProperties("second.csv", "67890fghijk",
                new Dictionary<string, string>
                {
                    { "other line1", "thing" },
                    { "other line2", "thing2" }
                }),
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 2, 2, 2, 2, 2, DateTimeKind.Utc)
        };
        var result = structuredMetadataConsolidator.BuildMetadataFields([inputMetadata], [documentMetadata]);

        result.ShouldBe([inputMetadata, documentMetadata]);
    }

    [Fact]
    public void BuildMetadataFields_Date_WithDifferentInputAndDocumentMetadataValues_ReturnsBoth()
    {
        var inputMetadata = new MetadataField<DateTime>
        {
            Id = Guid.NewGuid(),
            Name = MetadataFieldName.Date,
            Value = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 1, 1, 1, 1, 1, DateTimeKind.Utc)
        };
        var documentMetadata = new MetadataField<DateTime>
        {
            Id = Guid.NewGuid(),
            Name = MetadataFieldName.Date,
            Value = new DateTime(2022, 2, 2, 0, 0, 0, DateTimeKind.Utc),
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 2, 2, 2, 2, 2, DateTimeKind.Utc)
        };

        var result = structuredMetadataConsolidator.BuildMetadataFields([inputMetadata], [documentMetadata]);

        result.ShouldBe([inputMetadata, documentMetadata]);
    }

    [Fact]
    public void BuildMetadataFields_Judge_WithDifferentInputAndDocumentMetadataValues_ReturnsBoth()
    {
        var inputMetadata = new MetadataField<Judge>
        {
            Id = Guid.NewGuid(),
            Name = MetadataFieldName.Judge,
            Value = new Judge("Judge Alpha", "judge-judge-alpha"),
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 1, 1, 1, 1, 1, DateTimeKind.Utc)
        };
        var documentMetadata = new MetadataField<Judge>
        {
            Id = Guid.NewGuid(),
            Name = MetadataFieldName.Judge,
            Value = new Judge("Judge Beta", "judge-judge-beta"),
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 2, 2, 2, 2, 2, DateTimeKind.Utc)
        };
        var result = structuredMetadataConsolidator.BuildMetadataFields([inputMetadata], [documentMetadata]);

        result.ShouldBe([inputMetadata, documentMetadata]);
    }

    [Fact]
    public void BuildMetadataFields_Party_WithDifferentInputAndDocumentMetadataValues_ReturnsBoth()
    {
        var inputMetadata = new MetadataField<Party>
        {
            Id = Guid.NewGuid(),
            Name = MetadataFieldName.Party,
            Value = new Party
            {
                Name = "Alpha",
                Role = PartyRole.Defendant
            },
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 1, 1, 1, 1, 1, DateTimeKind.Utc)
        };
        var documentMetadata = new MetadataField<Party>
        {
            Id = Guid.NewGuid(),
            Name = MetadataFieldName.Party,
            Value = new Party
            {
                Name = "Beta",
                Role = PartyRole.Applicant
            },
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 2, 2, 2, 2, 2, DateTimeKind.Utc)
        };

        var result = structuredMetadataConsolidator.BuildMetadataFields([inputMetadata], [documentMetadata]);

        result.ShouldBe([inputMetadata, documentMetadata]);
    }

    [Theory]
    [InlineData(MetadataFieldName.CaseNumber)]
    [InlineData(MetadataFieldName.Court)]
    [InlineData(MetadataFieldName.HeadnoteSummary)]
    [InlineData(MetadataFieldName.Jurisdiction)]
    [InlineData(MetadataFieldName.Name)]
    [InlineData(MetadataFieldName.Ncn)]
    [InlineData(MetadataFieldName.SourceFormat)]
    [InlineData(MetadataFieldName.Uri)]
    [InlineData(MetadataFieldName.WebArchivingLink)]
    public void BuildMetadataFields_String_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth(
        MetadataFieldName metadataFieldName)
    {
        BuildMetadataFields_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth(new MetadataField<string>
        {
            Name = metadataFieldName,
            Value = "some value"
        });
    }

    [Fact]
    public void BuildMetadataFields_Category_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth()
    {
        BuildMetadataFields_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth(new MetadataField<Category>
        {
            Name = MetadataFieldName.Category,
            Value = new Category
            {
                Name = "My category",
                Parent = "Bigger category"
            }
        });
    }

    [Fact]
    public void BuildMetadataFields_CsvMetadataFileProperties_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth()
    {
        BuildMetadataFields_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth(new MetadataField<CsvProperties>
        {
            Name = MetadataFieldName.CsvMetadataFileProperties,
            Value = new CsvProperties("my.csv", "12345abcde", new Dictionary<string, string>
            {
                { "line1", "thing" },
                { "line2", "thing2" }
            })
        });
    }

    [Fact]
    public void BuildMetadataFields_Date_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth()
    {
        BuildMetadataFields_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth(new MetadataField<DateTime>
        {
            Name = MetadataFieldName.Date,
            Value = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    [Fact]
    public void BuildMetadataFields_Judge_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth()
    {
        BuildMetadataFields_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth(new MetadataField<Judge>
        {
            Name = MetadataFieldName.Judge,
            Value = new Judge("Judge Alpha", "judge-judge-alpha")
        });
    }

    [Fact]
    public void BuildMetadataFields_Party_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth()
    {
        BuildMetadataFields_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth(new MetadataField<Party>
        {
            Name = MetadataFieldName.Party,
            Value = new Party
            {
                Name = "Alpha",
                Role = PartyRole.Defendant
            }
        });
    }

    private void BuildMetadataFields_WithDifferentInputAndDocumentMetadataSources_ReturnsBoth<T>(
        MetadataField<T> template)
    {
        var inputMetadata = template with
        {
            Id = Guid.NewGuid(),
            Source = MetadataSource.Editor,
            Timestamp = new DateTime(2026, 1, 1, 1, 1, 1, DateTimeKind.Utc)
        };

        var documentMetadata = template with
        {
            Id = Guid.NewGuid(),
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 2, 2, 2, 2, 2, DateTimeKind.Utc)
        };

        var result = structuredMetadataConsolidator.BuildMetadataFields([inputMetadata], [documentMetadata]);

        result.ShouldBe([inputMetadata, documentMetadata]);
    }

    [Theory]
    [InlineData(MetadataFieldName.CaseNumber)]
    [InlineData(MetadataFieldName.Court)]
    [InlineData(MetadataFieldName.HeadnoteSummary)]
    [InlineData(MetadataFieldName.Jurisdiction)]
    [InlineData(MetadataFieldName.Name)]
    [InlineData(MetadataFieldName.Ncn)]
    [InlineData(MetadataFieldName.SourceFormat)]
    [InlineData(MetadataFieldName.Uri)]
    [InlineData(MetadataFieldName.WebArchivingLink)]
    public void BuildMetadataFields_String_WithSameInputAndDocumentMetadata_ReturnsInputMetadata(
        MetadataFieldName metadataFieldName)
    {
        BuildMetadataFields_WithSameInputAndDocumentMetadata_ReturnsInputMetadata(new MetadataField<string>
        {
            Name = metadataFieldName,
            Value = "some value"
        });
    }

    [Fact]
    public void BuildMetadataFields_Category_WithSameInputAndDocumentMetadata_ReturnsInputMetadata()
    {
        BuildMetadataFields_WithSameInputAndDocumentMetadata_ReturnsInputMetadata(new MetadataField<Category>
        {
            Name = MetadataFieldName.Category,
            Value = new Category
            {
                Name = "My category",
                Parent = "Bigger category"
            }
        });
    }

    [Fact]
    public void BuildMetadataFields_CsvMetadataFileProperties_WithSameInputAndDocumentMetadata_ReturnsInputMetadata()
    {
        BuildMetadataFields_WithSameInputAndDocumentMetadata_ReturnsInputMetadata(new MetadataField<CsvProperties>
        {
            Name = MetadataFieldName.CsvMetadataFileProperties,
            Value = new CsvProperties("my.csv", "12345abcde", new Dictionary<string, string>
            {
                { "line1", "thing" },
                { "line2", "thing2" }
            })
        });
    }

    [Fact]
    public void BuildMetadataFields_Date_WithSameInputAndDocumentMetadata_ReturnsInputMetadata()
    {
        BuildMetadataFields_WithSameInputAndDocumentMetadata_ReturnsInputMetadata(new MetadataField<DateTime>
        {
            Name = MetadataFieldName.Date,
            Value = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    [Fact]
    public void BuildMetadataFields_Judge_WithSameInputAndDocumentMetadata_ReturnsInputMetadata()
    {
        BuildMetadataFields_WithSameInputAndDocumentMetadata_ReturnsInputMetadata(new MetadataField<Judge>
        {
            Name = MetadataFieldName.Judge,
            Value = new Judge("Judge Alpha", "judge-judge-alpha")
        });
    }

    [Fact]
    public void BuildMetadataFields_Party_WithSameInputAndDocumentMetadata_ReturnsInputMetadata()
    {
        BuildMetadataFields_WithSameInputAndDocumentMetadata_ReturnsInputMetadata(new MetadataField<Party>
        {
            Name = MetadataFieldName.Party,
            Value = new Party
            {
                Name = "Alpha",
                Role = PartyRole.Defendant
            }
        });
    }

    private void BuildMetadataFields_WithSameInputAndDocumentMetadata_ReturnsInputMetadata<T>(MetadataField<T> template)
    {
        var inputMetadata = template with
        {
            Id = Guid.NewGuid(),
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 1, 1, 1, 1, 1, DateTimeKind.Utc)
        };

        var documentMetadata = template with
        {
            Id = Guid.NewGuid(),
            Source = MetadataSource.Document,
            Timestamp = new DateTime(2026, 2, 2, 2, 2, 2, DateTimeKind.Utc)
        };

        var result = structuredMetadataConsolidator.BuildMetadataFields([inputMetadata], [documentMetadata]);

        result.ShouldHaveSingleItem().ShouldBe(inputMetadata);
    }
}
