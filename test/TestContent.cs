
// using System.Collections.Generic;
// using System.Text.RegularExpressions;
// using System.Xml;

// using Xunit;

// using Api = UK.Gov.NationalArchives.Judgments.Api;
// using Hash = UK.Gov.Legislation.Judgments.AkomaNtoso.SHA256;

// namespace UK.Gov.NationalArchives.CaseLaw {

// public class TestContent {

//     public static IEnumerable<object[]> indices = Tests.indices;

//     [Theory]
//     [MemberData(nameof(indices))]
//     public void Test(int i) {
//         var docx = Tests.ReadDocx(i);
//         var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
//         var expected = Tests.ReadXml(i);
//         actual = ExtractNormalizedContent(actual);
//         expected = ExtractNormalizedContent(expected);
//         Assert.Equal(expected, actual);
//     }

//     [Fact]
//     public void Test11() {
//         var main = Tests.ReadDocx(11, "main");
//         var attach = Tests.ReadDocx(11, "order");
//         List<Api.Attachment> attachments = new List<Api.Attachment>(1) { new Api.Attachment() { Content = attach, Type = Api.AttachmentType.Order } };
//         var actual = Api.Parser.Parse(new Api.Request(){ Content = main, Attachments = attachments }).Xml;
//         var expected = Tests.ReadXml(11);
//         actual = ExtractNormalizedContent(actual);
//         expected = ExtractNormalizedContent(expected);
//         Assert.Equal(expected, actual);
//     }

//     [Fact]
//     public void Test28() {
//         var docx = Tests.ReadDocx(28);
//         var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
//         var expected = Tests.ReadXml(28);
//         actual = ExtractNormalizedContent(actual);
//         expected = ExtractNormalizedContent(expected);
//         Assert.Equal(expected, actual);
//     }

//     private string ExtractNormalizedContent(string xml) {
//         XmlDocument doc = new XmlDocument();
//         doc.LoadXml(xml);
//         string content = Hash.RemoveMetadata(doc);
//         return Regex.Replace(content, @"\s", "");
//     }

// }

// }
