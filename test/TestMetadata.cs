
// using System.Collections.Generic;
// using System.Xml;

// using Xunit;

// using Api = UK.Gov.NationalArchives.Judgments.Api;
// using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

// namespace UK.Gov.NationalArchives.CaseLaw {

// public class TestMetadata {

//     public static IEnumerable<object[]> indices = Tests.indices;

//     [Theory]
//     [MemberData(nameof(indices))]
//     public void Test(int i) {
//         var docx = Tests.ReadDocx(i);
//         var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
//         var expected = Tests.ReadXml(i);
//         TestMetadata1(expected, actual);
//     }

//     [Fact]
//     public void Test11() {
//         var main = Tests.ReadDocx(11, "main");
//         var attach = Tests.ReadDocx(11, "order");
//         List<Api.Attachment> attachments = new List<Api.Attachment>(1) { new Api.Attachment() { Content = attach, Type = Api.AttachmentType.Order } };
//         var actual = Api.Parser.Parse(new Api.Request(){ Content = main, Attachments = attachments }).Xml;
//         var expected = Tests.ReadXml(11);
//         TestMetadata1(expected, actual);
//     }

//     [Fact]
//     public void Test28() {
//         var docx = Tests.ReadDocx(28);
//         var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
//         var expected = Tests.ReadXml(28);
//         TestMetadata1(expected, actual);
//     }

//     private void TestMetadata1(string expected, string actual) {
//         XmlDocument expected1 = new XmlDocument();
//         expected1.LoadXml(expected);
//         AkN.Meta expectedMeta = AkN.MetadataExtractor.Extract(expected1);

//         XmlDocument actual1 = new XmlDocument();
//         actual1.LoadXml(actual);
//         AkN.Meta actualMeta = AkN.MetadataExtractor.Extract(actual1);

//         Assert.Equal("uri=" + expectedMeta.WorkUri, "uri=" + actualMeta.WorkUri);
//         Assert.Equal("date=" + expectedMeta.WorkDate, "date=" + actualMeta.WorkDate);
//         Assert.Equal("name=" + expectedMeta.WorkName, "name=" + actualMeta.WorkName);
//         Assert.Equal("court=" + expectedMeta.UKCourt, "court=" + actualMeta.UKCourt);
//         Assert.Equal("cite=" + expectedMeta.UKCite, "cite=" + actualMeta.UKCite);
//     }

// }

// }
