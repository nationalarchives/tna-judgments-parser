
// using System.Collections.Generic;
// using System.Xml;

// using Xunit;

// using Api = UK.Gov.NationalArchives.Judgments.Api;
// using AkN = UK.Gov.Legislation.Judgments.AkomaNtoso;

// namespace UK.Gov.NationalArchives.CaseLaw {

// public class TestHeaders {

//     public static IEnumerable<object[]> indices = Tests.indices;

//     [Theory]
//     [MemberData(nameof(indices))]
//     public void Test(int i) {
//         var docx = Tests.ReadDocx(i);
//         var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
//         var expected = Tests.ReadXml(i);
//         TestHeader(actual, expected);
//     }

//     [Fact]
//     public void Test11() {
//         var main = Tests.ReadDocx(11, "main");
//         var attach = Tests.ReadDocx(11, "order");
//         List<Api.Attachment> attachments = new List<Api.Attachment>(1) { new Api.Attachment() { Content = attach, Type = Api.AttachmentType.Order } };
//         var actual = Api.Parser.Parse(new Api.Request(){ Content = main, Attachments = attachments }).Xml;
//         var expected = Tests.ReadXml(11);
//         TestHeader(actual, expected);
//     }

//     [Fact]
//     public void Test28() {
//         var docx = Tests.ReadDocx(28);
//         var actual = Api.Parser.Parse(new Api.Request(){ Content = docx }).Xml;
//         var expected = Tests.ReadXml(28);
//         TestHeader(actual, expected);
//     }

//     private void TestHeader(string actual, string expected) {
//         XmlDocument actual1 = new XmlDocument();
//         actual1.LoadXml(actual);
//         XmlDocument expected1 = new XmlDocument();
//         expected1.LoadXml(expected);
//         XmlNamespaceManager nsmgr1 = new XmlNamespaceManager(actual1.NameTable);
//         nsmgr1.AddNamespace("akn", "http://docs.oasis-open.org/legaldocml/ns/akn/3.0");
//         nsmgr1.AddNamespace("uk", "https://caselaw.nationalarchives.gov.uk/akn");
//         var x = actual1.SelectNodes("/akn:akomaNtoso/akn:judgment/akn:header/*", nsmgr1);
//         var y = expected1.SelectNodes("/akn:akomaNtoso/akn:judgment/akn:header/*", nsmgr1);
//         Assert.Equal(x.Count, y.Count);
//     }

// }

// }
