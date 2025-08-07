
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Xunit;

namespace UK.Gov.Legislation.Lawmaker
{
    public class LawmakerTest
    {

        private static readonly int N = 10;

        public static readonly IEnumerable<object[]> Indices = Enumerable.Range(1, N)
            .Select(i => new object[] { i });

        [Theory]
        [MemberData(nameof(Indices))]
        public void Test(int i)
        {
            var docx = ReadDocx(i);
            var actual = Helper.Parse(docx, new LegislationClassifier(DocName.NIPUBB, null, null)).Xml;
            XmlDocument actualDoc = new();
            actualDoc.LoadXml(actual);

            var expected = ReadXml(i);
            XmlDocument expectedDoc = new();
            expectedDoc.LoadXml(expected);

            Assert.Equal(expectedDoc.OuterXml, actualDoc.OuterXml);
        }
        private static byte[] ReadDocx(int i)
        {
            var resource = $"test.lawmaker.test{i}.docx";
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resource);
            using MemoryStream ms = new();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static string ReadXml(int i)
        {
            var resource = $"test.lawmaker.test{i}.xml";
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resource);
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

    }

}
