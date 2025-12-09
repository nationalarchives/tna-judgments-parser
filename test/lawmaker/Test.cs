
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;
using Xunit;

namespace UK.Gov.Legislation.Lawmaker
{
    public class LawmakerTest
    {

        private static ILogger logger = Logging.Factory.CreateLogger<LawmakerTest>();

        private static string relativeTestPath = "../../../lawmaker";

        public static IEnumerable<object[]> TestFilePaths()
        {
            foreach (string filePath in Directory.GetFiles(relativeTestPath, "*.xml", SearchOption.AllDirectories))
            {
                string subdirectory = null;
                string filename = null;
                try
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetDirectoryName(filePath));
                    subdirectory = directoryInfo.Name;
                    filename = Path.GetFileNameWithoutExtension(filePath);
                }
                catch (Exception) { }

                if (String.IsNullOrEmpty(subdirectory) || String.IsNullOrEmpty(filename) || filename.StartsWith("~$") || filename.StartsWith('_'))
                {
                    logger.LogWarning($"Invalid filepath: {filePath}. Ignoring test.");
                    continue;
                }
                yield return new object[] { subdirectory + "/" + filename };
            }
        }

        [Theory]
        [MemberData(nameof(TestFilePaths))]
        public void Test(string test)
        {
            String[] parts = test.Split('/');
            string subdirectory = parts[0];
            string filename = parts[1];

            DocName docName;
            try
            {
                DocName? tmp = DocNames.GetDocName(subdirectory);
                if (tmp == null) throw new Exception();
                docName = (DocName)tmp;
            }
            catch
            {
                throw new IOException($"DocName could not be determined from subdirectory {subdirectory}.");
            }

            var docx = ReadDocx(subdirectory, filename);
            LegislationClassifier classifier = new LegislationClassifier(docName, null, null);
            LanguageService languageService = new LanguageService(["en", "cy"]);
            var actual = Helper.Parse(docx, classifier, languageService).Xml;
            XmlDocument actualDoc = new();
            actualDoc.LoadXml(actual);

            var expected = ReadXml(subdirectory, filename);
            XmlDocument expectedDoc = new();
            expectedDoc.LoadXml(expected);

            Assert.Equal(expectedDoc.OuterXml, actualDoc.OuterXml);
        }
        private static byte[] ReadDocx(string subdirectory, string filename)
        {
            var resource = $"test.lawmaker.{subdirectory}.{filename}.docx";
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resource);
            if (stream == null)
                throw new FileNotFoundException($"{subdirectory}/{filename}.docx could not be found.");
            using MemoryStream ms = new();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static string ReadXml(string subdirectory, string filename)
        {
            var resource = $"test.lawmaker.{subdirectory}.{filename}.xml";
            var assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(resource);
            if (stream == null)
                throw new FileNotFoundException($"{subdirectory}/{filename}.xml could not be found.");
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

    }

}
