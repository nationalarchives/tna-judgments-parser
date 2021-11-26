
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Crypto = System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Xsl;

using Microsoft.Extensions.Logging;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class SHA256 {

    private static string xslt = @"<?xml version='1.0'?>
    <xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' xmlns:akn='http://docs.oasis-open.org/legaldocml/ns/akn/3.0' version='1.0'>
        <xsl:output method='text'/>
        <xsl:template match='akn:meta'/>
    </xsl:stylesheet>";

    private static string RemoveMetadata(XmlDocument akn) {
        XslCompiledTransform transform = new XslCompiledTransform();
        var stringReader = new StringReader(xslt);
        XmlReader xsltReader = XmlReader.Create(stringReader);
        transform.Load(xsltReader);
        XmlReader aknReader = new XmlNodeReader(akn);
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
