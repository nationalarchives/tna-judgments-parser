
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

        public static IEnumerable<object[]> Filenames()
        {
            string relativeTestPath = "../../../lawmaker";
            foreach (string filePath in Directory.GetFiles(relativeTestPath, "*.docx"))
            {
                yield return new object[] { Path.GetFileNameWithoutExtension(filePath) };
            }
        }

        [Theory]
        [MemberData(nameof(Filenames))]
        public void Test(string filename)
        {
            var docx = ReadDocx(filename);
            var actual = Helper.Parse(docx, new LegislationClassifier(DocName.NIPUBB, null, null)).Xml;
            XmlDocument actualDoc = new();
            actualDoc.LoadXml(actual);

            var expected = ReadXml(filename);
            XmlDocument expectedDoc = new();
            expectedDoc.LoadXml(expected);

            Assert.Equal(expectedDoc.OuterXml, actualDoc.OuterXml);
        }
        private static byte[] ReadDocx(string filename)
        {
            var resource = $"test.lawmaker.{filename}.docx";
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resource);
            using MemoryStream ms = new();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static string ReadXml(string filename)
        {
            var resource = $"test.lawmaker.{filename}.xml";
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resource);
            if (stream == null)
                throw new FileNotFoundException($"{filename}.xml could not be found.");
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

    }

}
