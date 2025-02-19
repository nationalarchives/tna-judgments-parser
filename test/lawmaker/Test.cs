
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Xunit;

namespace UK.Gov.Legislation.Lawmaker
{
    public class LawmakerTest
    {

        private static readonly int N = 3;

        public static readonly IEnumerable<object[]> Indices = Enumerable.Range(1, N)
            .Select(i => new object[] { i });

        [Theory]
        [MemberData(nameof(Indices))]
        public void Test(int i)
        {
            var docx = ReadDocx(i);
            var actual = Helper.Parse(docx).Xml;
            var expected = ReadXml(i);
            Assert.Equal(expected, actual);
        }

        /* helpers */


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
