
using System.IO;
using Crypto = System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Xsl;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public class SHA256 {

    private static string xslt = @"<?xml version='1.0'?>
    <xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' version='1.0'>
        <xsl:output method='text'/>
        <xsl:template match='akn:meta'/>
    </xsl:stylesheet>";

    public static string RemoveMetadata(XmlDocument akn) {
        XslCompiledTransform transform = new XslCompiledTransform();
        using var stringReader = new StringReader(xslt);
        using var xsltReader = XmlReader.Create(stringReader);
        transform.Load(xsltReader);
        using var aknReader = new XmlNodeReader(akn);
        var textWriter = new StringWriter();
        transform.Transform(aknReader, null, textWriter);
        textWriter.Close(); // necessary
        return textWriter.ToString();
    }

    internal static string Hash(XmlDocument akn) {
        string text = RemoveMetadata(akn);
        text = Regex.Replace(text, @"\s", "");
        Crypto.SHA256 sha256 = Crypto.SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
        StringWriter writer = new StringWriter();
        for (int i = 0; i < bytes.Length; i++)
            writer.Write($"{bytes[i]:X2}");
        return writer.ToString().ToLower();
    }

}

}
