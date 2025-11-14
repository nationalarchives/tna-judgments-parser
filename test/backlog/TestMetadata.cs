using System;
using NUnit.Framework;

using UK.Gov.Legislation.Judgments;
using Backlog.Src;

namespace Backlog.Test
{
    [TestFixture]
    public class TestMetadata
    {
        [Test]
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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Type, Is.EqualTo(JudgmentType.Decision));
            Assert.That(result.Court, Is.EqualTo(Courts.FirstTierTribunal_GRC));
            Assert.That(result.Date.Date, Is.EqualTo("2023-01-14"));
            Assert.That(result.Date.Name, Is.EqualTo("decision"));
            Assert.That(result.Name, Is.EqualTo("John Smith v HMRC"));
            Assert.That(result.CaseNumbers, Is.Not.Null);
            Assert.That(result.CaseNumbers.Count, Is.EqualTo(1));
            Assert.That(result.CaseNumbers[0], Is.EqualTo("ABC/2023/001"));
            Assert.That(result.SourceFormat, Is.EqualTo("application/pdf"));
            Assert.That(result.Parties.Count, Is.EqualTo(2));
            
            var firstParty = result.Parties.Find(p => p.Role == PartyRole.Claimant);
            var secondParty = result.Parties.Find(p => p.Role == PartyRole.Respondent);
            
            Assert.That(firstParty, Is.Not.Null);
            Assert.That(firstParty.Name, Is.EqualTo("John Smith"));
            Assert.That(secondParty, Is.Not.Null);
            Assert.That(secondParty.Name, Is.EqualTo("HMRC"));
        }

        [Test]
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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Type, Is.EqualTo(JudgmentType.Decision));
            Assert.That(result.Court, Is.EqualTo(Courts.FirstTierTribunal_GRC));
            Assert.That(result.Date.Date, Is.EqualTo("2023-01-14"));
            Assert.That(result.Date.Name, Is.EqualTo("decision"));
            Assert.That(result.Name, Is.EqualTo("Jane Doe v Home Office"));
            Assert.That(result.CaseNumbers, Is.Not.Null);
            Assert.That(result.CaseNumbers.Count, Is.EqualTo(1));
            Assert.That(result.CaseNumbers[0], Is.EqualTo("ABC/2023/002"));
            Assert.That(result.SourceFormat, Is.EqualTo("application/pdf"));
            Assert.That(result.Parties.Count, Is.EqualTo(2));
            
            var firstParty = result.Parties.Find(p => p.Role == PartyRole.Appellant);
            var secondParty = result.Parties.Find(p => p.Role == PartyRole.Respondent);
            
            Assert.That(firstParty, Is.Not.Null);
            Assert.That(firstParty.Name, Is.EqualTo("Jane Doe"));
            Assert.That(secondParty, Is.Not.Null);
            Assert.That(secondParty.Name, Is.EqualTo("Home Office"));
        }

        [Test]
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
            Assert.That(result.SourceFormat, Is.EqualTo("application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
        }

        [Test]
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
            Assert.That(result.SourceFormat, Is.EqualTo("application/vnd.openxmlformats-officedocument.wordprocessingml.document"));
        }

        [Test]
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
            Assert.That(ex.Message, Is.EqualTo("Unexpected extension .txt"));
        }

        [Test]
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
            Assert.That(result.Court, Is.EqualTo(Courts.FirstTierTribunal_GRC));
        }

        [Test]
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
            Assert.That(result.Parties, Is.Not.Null);
            Assert.That(result.Parties.Count, Is.EqualTo(2));
            
            var claimant = result.Parties.Find(p => p.Role == PartyRole.Claimant);
            var respondent = result.Parties.Find(p => p.Role == PartyRole.Respondent);
            
            Assert.That(claimant, Is.Not.Null);
            Assert.That(claimant.Name, Is.EqualTo("Jane Doe & John Smith"));
            
            Assert.That(respondent, Is.Not.Null);
            Assert.That(respondent.Name, Is.EqualTo("Home Office"));
        }

        [Test]
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
            Assert.That(result.Parties, Is.Not.Null);
            Assert.That(result.Parties.Count, Is.EqualTo(2));
            
            var appellant = result.Parties.Find(p => p.Role == PartyRole.Appellant);
            var respondent = result.Parties.Find(p => p.Role == PartyRole.Respondent);
            
            Assert.That(appellant, Is.Not.Null);
            Assert.That(appellant.Name, Is.EqualTo("Jane Doe & John Smith"));
            
            Assert.That(respondent, Is.Not.Null);
            Assert.That(respondent.Name, Is.EqualTo("Home Office"));
        }

        [Test]
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
            Assert.That(result.Categories, Is.Not.Null);
            Assert.That(result.Categories.Count, Is.EqualTo(4));

            // Check main category and subcategory
            var mainCategory = result.Categories.Find(c => c.Name == "Immigration" && c.Parent == null);
            var mainSubcategory = result.Categories.Find(c => c.Name == "Asylum" && c.Parent == "Immigration");
            
            Assert.That(mainCategory, Is.Not.Null);
            Assert.That(mainSubcategory, Is.Not.Null);

            // Check secondary category and subcategory
            var secCategory = result.Categories.Find(c => c.Name == "Human Rights" && c.Parent == null);
            var secSubcategory = result.Categories.Find(c => c.Name == "Article 8" && c.Parent == "Human Rights");
            
            Assert.That(secCategory, Is.Not.Null);
            Assert.That(secSubcategory, Is.Not.Null);
        }

        [Test]
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
            Assert.That(result.Categories, Is.Not.Null);
            Assert.That(result.Categories.Count, Is.EqualTo(2));

            // Check only main category and subcategory exist
            var mainCategory = result.Categories.Find(c => c.Name == "Immigration" && c.Parent == null);
            var mainSubcategory = result.Categories.Find(c => c.Name == "Asylum" && c.Parent == "Immigration");
            
            Assert.That(mainCategory, Is.Not.Null);
            Assert.That(mainSubcategory, Is.Not.Null);
        }

        [Test]
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
            Assert.That(result.Categories, Is.Not.Null);
            Assert.That(result.Categories.Count, Is.EqualTo(2));

            // Check only main category and subcategory exist
            var mainCategory = result.Categories.Find(c => c.Name == "Immigration" && c.Parent == null);
            var mainSubcategory = result.Categories.Find(c => c.Name == "Asylum" && c.Parent == "Immigration");
            
            Assert.That(mainCategory, Is.Not.Null);
            Assert.That(mainSubcategory, Is.Not.Null);
        }

        [Test]
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
            Assert.That(result.CaseNumbers[0], Is.EqualTo("IA/12345/2023"));
        }

        [Test]
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
            Assert.That(result.Date.Date, Is.EqualTo("2023-12-25"));
            Assert.That(result.Date.Name, Is.EqualTo("decision"));
        }

        [Test]
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
            Assert.That(decisionDate, Is.EqualTo("2023-07-04"));
        }


        [Test]
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

        [Test]
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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.NCN, Is.EqualTo("[2023] UKUT 123 (IAC)"));
        }

        [Test]
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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.NCN, Is.Null);
        }

        [Test]
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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.NCN, Is.EqualTo(""));
        }

        [Test]
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
            Assert.That(result, Is.Not.Null);
            Assert.That(result.NCN, Is.EqualTo("   "));
        }

        [Test]
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
            Assert.That(xml, Does.Contain("<uk:cite>[2023] UKUT 123 (IAC)</uk:cite>"));
        }

        [Test]
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
            Assert.That(xml, Does.Not.Contain("<uk:cite"));
            Assert.That(xml, Does.Not.Contain("</uk:cite>"));
        }

        [Test]
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
            Assert.That(xml, Does.Not.Contain("<uk:cite"));
            Assert.That(xml, Does.Not.Contain("</uk:cite>"));
        }

        [Test]
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
            Assert.That(xml, Does.Not.Contain("<uk:cite"));
            Assert.That(xml, Does.Not.Contain("</uk:cite>"));
        }

        [Test]
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
            Assert.That(xml, Does.Contain("<uk:cite"));
            Assert.That(xml, Does.Contain("[2023] EWCA Civ 123 &amp; 124"));
            Assert.That(xml, Does.Contain("</uk:cite>"));
        }

        [Test]
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
            Assert.That(ex.Message, Does.Contain("Cannot have both claimants and appellants"));
        }

        [Test]
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
            Assert.That(ex.Message, Does.Contain("Must have either claimants or appellants"));
        }

        [Test]
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
            Assert.That(result, Is.EqualTo("John Smith"));
        }

        [Test]
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
            Assert.That(result, Is.EqualTo("Jane Doe"));
        }

        [Test]
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
            Assert.That(result, Is.EqualTo(PartyRole.Claimant));
        }

        [Test]
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
            Assert.That(result, Is.EqualTo(PartyRole.Appellant));
        }
    }
}
