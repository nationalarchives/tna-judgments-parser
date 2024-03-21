
using System.Xml.Xsl;
using System.IO;
using System.Xml;
using Xunit;

using Api = UK.Gov.NationalArchives.Judgments.Api;

namespace UK.Gov.NationalArchives.CaseLaw.TRE.Test
{
    public class TestNoChange
    {

        public static readonly System.Collections.Generic.IEnumerable<object[]> Indices = Tests.indices;

        // [Theory]
        // [MemberData(nameof(Indices))]
        public static void TestJudgments(int i)
        {
            byte[] docx = Tests.ReadDocx(i);
            Api.Response response1 = TestInputInjection.LambdaTest(docx, null);
            ParserInputs inputs = new()
            {
                DocumentType = response1.Meta.DocumentType,
                Metadata = new InputMetadata()
                {
                    URI = Api.URI.ExtractShortURIComponent(response1.Meta.Uri),
                    Cite = response1.Meta.Cite,
                    Court = response1.Meta.Court,
                    Date = response1.Meta.Date,
                    Name = response1.Meta.Name
                }
            };
            Api.Response response2 = TestInputInjection.LambdaTest(docx, inputs);
            Assert.Equal(response1.Meta.Uri, response2.Meta.Uri);
            Assert.Equal(response1.Meta.Cite, response2.Meta.Cite);
            Assert.Equal(response1.Meta.Court, response2.Meta.Court);
            Assert.Equal(response1.Meta.Date, response2.Meta.Date);
            Assert.Equal(response1.Meta.Name, response2.Meta.Name);
            string actual = FixMetadata(response1.Xml);
            string expected = FixMetadata(response2.Xml);
            Assert.Equal(expected, actual);
        }

        /* if the parser is given a date, it can't tell whether it's a 'decision' or a 'hearing' date */

        private static string FixMetadata(string akn) {
            using XmlReader reader = XmlReader.Create(new StringReader(akn));
            using StringWriter sWriter = new StringWriter();
            using XmlWriter xWriter = XmlWriter.Create(sWriter);
            Transform.Transform(reader, xWriter);
            return sWriter.ToString();
        }

        private static readonly XslCompiledTransform Transform = new();

        static TestNoChange() {
            using var stringReader = new StringReader(XSLT);
            using var xsltReader = XmlReader.Create(stringReader);
            Transform.Load(xsltReader);
        }

        private static readonly string XSLT = @"<?xml version='1.0'?>
<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' xmlns:uk='https://caselaw.nationalarchives.gov.uk/akn'>
  <xsl:template match='akn:FRBRManifestation/akn:FRBRdate/@date'/>
  <xsl:template match=""akn:FRBRdate/@name[.='hearing']"">
    <xsl:attribute name=""name"">decision</xsl:attribute>
  </xsl:template>
  <xsl:template match='@*|node()'>
    <xsl:copy>
      <xsl:apply-templates select='@*|node()'/>
    </xsl:copy>
  </xsl:template>
</xsl:stylesheet>";

    }

}
