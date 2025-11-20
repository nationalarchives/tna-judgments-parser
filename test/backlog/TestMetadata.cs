using System;

using UK.Gov.Legislation.Judgments;

using Backlog.Src;

using Xunit;

namespace Backlog.Test
{
    public class TestMetadata
    {
        [Fact]
        public void MakeMetadata_WithBasicLine_CreatesCorrectMetadata()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "123",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                headnote_summary = "This is a test headnote summary",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                sec_category = "Human Rights",
                sec_subcategory = "Article 8",
                FilePath = "/path/to/test-document.pdf",
                Extension = ".pdf",
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(JudgmentType.Decision, result.Type);
            Assert.Equal(Courts.FirstTierTribunal_GRC, result.Court);
            Assert.Equal("2023-01-14", result.Date.Date);
            Assert.Equal("decision", result.Date.Name);
            Assert.Equal("John Smith v HMRC", result.Name);
            Assert.NotNull(result.CaseNumbers);
            var caseNumber = Assert.Single(result.CaseNumbers);
            Assert.Equal("ABC/2023/001", caseNumber);
            Assert.Equal("application/pdf", result.SourceFormat);
            Assert.Equal(2, result.Parties.Count);
            
            var firstParty = result.Parties.Find(p => p.Role == PartyRole.Claimant);
            var secondParty = result.Parties.Find(p => p.Role == PartyRole.Respondent);
            
            Assert.NotNull(firstParty);
            Assert.Equal("John Smith", firstParty.Name);
            Assert.NotNull(secondParty);
            Assert.Equal("HMRC", secondParty.Name);
        }

        [Fact]
        public void MakeMetadata_WithAppellants_CreatesCorrectMetadata()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "124",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/002",
                appellants = "Jane Doe",
                respondent = "Home Office",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                Extension = ".pdf"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(JudgmentType.Decision, result.Type);
            Assert.Equal(Courts.FirstTierTribunal_GRC, result.Court);
            Assert.Equal("2023-01-14", result.Date.Date);
            Assert.Equal("decision", result.Date.Name);
            Assert.Equal("Jane Doe v Home Office", result.Name);
            Assert.NotNull(result.CaseNumbers);
            var caseNumber = Assert.Single(result.CaseNumbers);
            Assert.Equal("ABC/2023/002", caseNumber);
            Assert.Equal("application/pdf", result.SourceFormat);
            Assert.Equal(2, result.Parties.Count);
            
            var firstParty = result.Parties.Find(p => p.Role == PartyRole.Appellant);
            var secondParty = result.Parties.Find(p => p.Role == PartyRole.Respondent);
            
            Assert.NotNull(firstParty);
            Assert.Equal("Jane Doe", firstParty.Name);
            Assert.NotNull(secondParty);
            Assert.Equal("Home Office", secondParty.Name);
        }

        [Fact]
        public void MakeMetadata_WithDocxFile_SetsCorrectSourceFormat()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                Extension = ".docx"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.SourceFormat);
        }

        [Fact]
        public void MakeMetadata_WithDocFile_SetsCorrectSourceFormat()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                Extension = ".doc"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.SourceFormat);
        }

        [Fact]
        public void MakeMetadata_WithUnsupportedExtension_ThrowsException()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                Extension = ".txt"
            };

            // Act & Assert
            var ex = Assert.Throws<Exception>(() => Metadata.MakeMetadata(line));
            Assert.Equal("Unexpected extension .txt", ex.Message);
        }

        [Fact]
        public void MakeMetadata_WithNewDate_UsesFirstTierTribunal()
        {
            // Arrange - Date on or after 2010-01-18
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2010-01-18 14:30:00",
                CaseNo = "ABC/2010/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                Extension = ".pdf"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.Equal(Courts.FirstTierTribunal_GRC, result.Court);
        }

        [Fact]
        public void MakeMetadata_WithPartiesData_CreatesCorrectParties()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "Jane Doe & John Smith",
                respondent = "Home Office",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                Extension = ".pdf"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result.Parties);
            Assert.Equal(2, result.Parties.Count);
            
            var claimant = result.Parties.Find(p => p.Role == PartyRole.Claimant);
            var respondent = result.Parties.Find(p => p.Role == PartyRole.Respondent);
            
            Assert.NotNull(claimant);
            Assert.Equal("Jane Doe & John Smith", claimant.Name);
            
            Assert.NotNull(respondent);
            Assert.Equal("Home Office", respondent.Name);
        }

        [Fact]
        public void MakeMetadata_WithAppellantPartiesData_CreatesCorrectParties()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                appellants = "Jane Doe & John Smith",
                respondent = "Home Office",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                Extension = ".pdf"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result.Parties);
            Assert.Equal(2, result.Parties.Count);
            
            var appellant = result.Parties.Find(p => p.Role == PartyRole.Appellant);
            var respondent = result.Parties.Find(p => p.Role == PartyRole.Respondent);
            
            Assert.NotNull(appellant);
            Assert.Equal("Jane Doe & John Smith", appellant.Name);
            
            Assert.NotNull(respondent);
            Assert.Equal("Home Office", respondent.Name);
        }

        [Fact]
        public void MakeMetadata_WithCategoriesData_CreatesCorrectCategories()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                sec_category = "Human Rights",
                sec_subcategory = "Article 8",
                Extension = ".pdf"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result.Categories);
            Assert.Equal(4, result.Categories.Count);

            // Check main category and subcategory
            var mainCategory = result.Categories.Find(c => c.Name == "Immigration" && c.Parent == null);
            var mainSubcategory = result.Categories.Find(c => c.Name == "Asylum" && c.Parent == "Immigration");
            
            Assert.NotNull(mainCategory);
            Assert.NotNull(mainSubcategory);

            // Check secondary category and subcategory
            var secCategory = result.Categories.Find(c => c.Name == "Human Rights" && c.Parent == null);
            var secSubcategory = result.Categories.Find(c => c.Name == "Article 8" && c.Parent == "Human Rights");
            
            Assert.NotNull(secCategory);
            Assert.NotNull(secSubcategory);
        }

        [Fact]
        public void MakeMetadata_WithoutSecondaryCategory_CreatesOnlyMainCategories()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                sec_category = null, // No secondary category
                sec_subcategory = null,
                Extension = ".pdf"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result.Categories);
            Assert.Equal(2, result.Categories.Count);

            // Check only main category and subcategory exist
            var mainCategory = result.Categories.Find(c => c.Name == "Immigration" && c.Parent == null);
            var mainSubcategory = result.Categories.Find(c => c.Name == "Asylum" && c.Parent == "Immigration");
            
            Assert.NotNull(mainCategory);
            Assert.NotNull(mainSubcategory);
        }

        [Fact]
        public void MakeMetadata_WithWhitespaceSecondaryCategory_CreatesOnlyMainCategories()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                sec_category = "   ", // Whitespace only
                sec_subcategory = "Article 8",
                Extension = ".pdf"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result.Categories);
            Assert.Equal(2, result.Categories.Count);

            // Check only main category and subcategory exist
            var mainCategory = result.Categories.Find(c => c.Name == "Immigration" && c.Parent == null);
            var mainSubcategory = result.Categories.Find(c => c.Name == "Asylum" && c.Parent == "Immigration");
            
            Assert.NotNull(mainCategory);
            Assert.NotNull(mainSubcategory);
        }

        [Fact]
        public void MakeMetadata_WithComplexFileNumbers_CreatesCorrectCaseNumber()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "IA/12345/2023",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                Extension = ".pdf"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.Equal("IA/12345/2023", result.CaseNumbers[0]);
        }

        [Fact]
        public void MakeMetadata_DecisionDate_ParsesCorrectly()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-12-25 15:45:30",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                main_subcategory = "Asylum",
                Extension = ".pdf"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.Equal("2023-12-25", result.Date.Date);
            Assert.Equal("decision", result.Date.Name);
        }

        [Fact]
        public void Line_DecisionDate_Property_ParsesCorrectly()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "2023-07-04 09:15:22"
            };

            // Act
            var decisionDate = line.DecisionDate;

            // Assert
            Assert.Equal("2023-07-04", decisionDate);
        }


        [Fact]
        public void Line_DecisionDate_Property_WithInvalidDate_ThrowsException()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                decision_datetime = "invalid-date"
            };

            // Act & Assert
            Assert.Throws<FormatException>(() => _ = line.DecisionDate);
        }

        [Fact]
        public void MakeMetadata_WithNCN_SetsNCNProperty()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "123",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                Extension = ".pdf",
                ncn = "[2023] UKUT 123 (IAC)"
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("[2023] UKUT 123 (IAC)", result.NCN);
        }

        [Fact]
        public void MakeMetadata_WithoutNCN_NCNPropertyIsNull()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "123",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                Extension = ".pdf"
                // ncn is not set
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result);
            Assert.Null(result.NCN);
        }

        [Fact]
        public void MakeMetadata_WithEmptyNCN_NCNPropertyIsEmpty()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "123",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                Extension = ".pdf",
                ncn = ""
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("", result.NCN);
        }

        [Fact]
        public void MakeMetadata_WithWhitespaceNCN_NCNPropertyIsWhitespace()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "123",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                Extension = ".pdf",
                ncn = "   "
            };

            // Act
            var result = Metadata.MakeMetadata(line);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("   ", result.NCN);
        }

        [Fact]
        public void Stub_WithNCN_AppearsInXmlAsCite()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "123",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                Extension = ".pdf",
                ncn = "[2023] UKUT 123 (IAC)"
            };
            var metadata = Metadata.MakeMetadata(line);

            // Act
            var stub = Stub.Make(metadata);
            var xml = stub.Serialize();

            // Assert
            Assert.Contains("<uk:cite>[2023] UKUT 123 (IAC)</uk:cite>", xml);
        }

        [Fact]
        public void Stub_WithEmptyNCN_DoesNotAppearInXml()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "123",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                Extension = ".pdf",
                ncn = ""
            };
            var metadata = Metadata.MakeMetadata(line);

            // Act
            var stub = Stub.Make(metadata);
            var xml = stub.Serialize();

            // Assert
            Assert.DoesNotContain("<uk:cite", xml);
            Assert.DoesNotContain("</uk:cite>", xml);
        }

        [Fact]
        public void Stub_WithNullNCN_DoesNotAppearInXml()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "123",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                Extension = ".pdf"
                // ncn is not set (null)
            };
            var metadata = Metadata.MakeMetadata(line);

            // Act
            var stub = Stub.Make(metadata);
            var xml = stub.Serialize();

            // Assert
            Assert.DoesNotContain("<uk:cite", xml);
            Assert.DoesNotContain("</uk:cite>", xml);
        }

        [Fact]
        public void Stub_WithWhitespaceNCN_DoesNotAppearInXml()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "123",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                Extension = ".pdf",
                ncn = "   "
            };
            var metadata = Metadata.MakeMetadata(line);

            // Act
            var stub = Stub.Make(metadata);
            var xml = stub.Serialize();

            // Assert
            Assert.DoesNotContain("<uk:cite", xml);
            Assert.DoesNotContain("</uk:cite>", xml);
        }

        [Fact]
        public void Stub_WithNCNSpecialCharacters_AppearsCorrectlyInXml()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "123",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/001",
                claimants = "John Smith",
                respondent = "HMRC",
                main_category = "Immigration",
                Extension = ".pdf",
                ncn = "[2023] EWCA Civ 123 & 124"
            };
            var metadata = Metadata.MakeMetadata(line);

            // Act
            var stub = Stub.Make(metadata);
            var xml = stub.Serialize();

            // Assert
            Assert.Contains("<uk:cite", xml);
            Assert.Contains("[2023] EWCA Civ 123 &amp; 124", xml);
            Assert.Contains("</uk:cite>", xml);
        }

        [Fact]
        public void Line_ValidateCategoryRules_WithBothClaimantsAndAppellants_ThrowsException()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "125",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/003",
                claimants = "John Smith",
                appellants = "Jane Doe", // Both provided - should fail
                respondent = "HMRC",
                Extension = ".pdf"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => line.ValidateCategoryRules());
            Assert.Contains("Cannot have both claimants and appellants", ex.Message);
        }

        [Fact]
        public void Line_ValidateCategoryRules_WithNeitherClaimantsNorAppellants_ThrowsException()
        {
            // Arrange
            var line = new Metadata.Line
            {
                id = "126",
                court = "UKFTT-GRC",
                decision_datetime = "2023-01-14 14:30:00",
                CaseNo = "ABC/2023/004",
                // Neither claimants nor appellants provided - should fail
                respondent = "HMRC",
                Extension = ".pdf"
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => line.ValidateCategoryRules());
            Assert.Contains("Must have either claimants or appellants", ex.Message);
        }

        [Fact]
        public void Line_FirstPartyName_WithClaimants_ReturnsClaimants()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                claimants = "John Smith",
                respondent = "HMRC"
            };

            // Act
            var result = line.FirstPartyName;

            // Assert
            Assert.Equal("John Smith", result);
        }

        [Fact]
        public void Line_FirstPartyName_WithAppellants_ReturnsAppellants()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                appellants = "Jane Doe",
                respondent = "HMRC"
            };

            // Act
            var result = line.FirstPartyName;

            // Assert
            Assert.Equal("Jane Doe", result);
        }

        [Fact]
        public void Line_FirstPartyRole_WithClaimants_ReturnsClaimant()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                claimants = "John Smith",
                respondent = "HMRC"
            };

            // Act
            var result = line.FirstPartyRole;

            // Assert
            Assert.Equal(PartyRole.Claimant, result);
        }

        [Fact]
        public void Line_FirstPartyRole_WithAppellants_ReturnsAppellant()
        {
            // Arrange
            var line = new Metadata.Line
            {
                court = "UKFTT-GRC",
                appellants = "Jane Doe",
                respondent = "HMRC"
            };

            // Act
            var result = line.FirstPartyRole;

            // Assert
            Assert.Equal(PartyRole.Appellant, result);
        }
    }
}
