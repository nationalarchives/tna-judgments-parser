
using System.IO;
using System.Xml;

namespace UK.Gov.Legislation.Lawmaker
{

    public class Helper
    {

        public static Bundle ParseFile(string path)
        {
            byte[] docx = File.ReadAllBytes(path);
            return Parse(docx);
        }

        public static Bundle Parse(byte[] docx)
        {
            Bill bill = BillParser.Parse(docx);
            XmlDocument doc = Builder.Build(bill);
            Simplifier.Simplify(doc, bill.Styles);
            string xml = NationalArchives.Judgments.Api.Parser.SerializeXml(doc);
            return new Bundle { Xml = xml };
        }

    }

}
