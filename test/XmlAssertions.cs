#nullable enable

using System;
using System.Linq;
using System.Xml;

using Xunit;

namespace test;

public static class XmlAssertions
{
    public static XmlNode HasSingleNodeWithName(this XmlDocument doc, string name)
    {
        var nodeMatches = doc.GetElementsByTagName(name);
        Assert.NotNull(nodeMatches);
        Assert.Single(nodeMatches);

        return nodeMatches[0]!;
    }

    public static void DoesNotHaveNodeWithName(this XmlDocument doc, string name)
    {
        var nodeMatches = doc.GetElementsByTagName(name);
        Assert.NotNull(nodeMatches);
        Assert.Empty(nodeMatches);
    }

    public static void Match(this XmlNode node, string expectedName, string expectedValue,
        params (string key, string value)[] expectedAttributes)
    {
        Assert.Equal(expectedName, node.Name);
        Assert.Equal(expectedValue, node.InnerText);

        Assert.Equal(expectedAttributes.Length, node.Attributes?.Count ?? 0);
        var actualAttributes = node.Attributes!;
        foreach (var (expectedAttributeKey, expectedAttributeValue) in expectedAttributes)
        {
            Assert.NotNull(actualAttributes[expectedAttributeKey]);
            Assert.Equal(expectedAttributeValue, actualAttributes[expectedAttributeKey]!.InnerText);
        }
    }

    public static XmlNode HaveName(this XmlNode node, string expectedName)
    {
        Assert.Equal(expectedName, node.Name);
        return node;
    }

    public static XmlNode HaveValueMatching(this XmlNode node, string expectedValueRegex)
    {
        Assert.Matches(expectedValueRegex, node.InnerText);
        return node;
    }

    public static void ThatMatch(this XmlAttributeCollection? attributes,
        params (string key, string value)[] expectedAttributes)
    {
        Assert.Equal(expectedAttributes.Length, attributes?.Count ?? 0);

        var actualAttributes = attributes!;
        foreach (var (expectedAttributeKey, expectedAttributeValue) in expectedAttributes)
        {
            Assert.NotNull(actualAttributes[expectedAttributeKey]);
            Assert.Equal(expectedAttributeValue, actualAttributes[expectedAttributeKey]!.InnerText);
        }
    }

    public static void HasChildrenMatching(this XmlNode node, params Action<XmlNode>[] childInspectors)
    {
        var referencesNodeChildren = node.Cast<XmlNode>();

        Assert.Collection(referencesNodeChildren, childInspectors);
    }

    #region Readability No Ops
    
    public static XmlNode And(this XmlNode node) => node;
    public static XmlNode Should(this XmlNode node) => node;
    public static XmlNode Which(this XmlNode node) => node;

    #endregion

}
