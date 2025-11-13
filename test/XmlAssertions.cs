#nullable enable

using System;
using System.Linq;
using System.Xml;

using Xunit;

namespace test;

public static class XmlAssertions
{
    public static XmlNode HasSingleNodeMatching(this XmlDocument doc, string name)
    {
        var nodeMatches = doc.GetElementsByTagName(name);
        Assert.NotNull(nodeMatches);
        Assert.Single(nodeMatches);

        return nodeMatches[0]!;
    }
    
    public static void ShouldMatch(this XmlNode node, string expectedName, string expectedValue, params (string key, string value)[] expectedAttributes)
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
    
    public static XmlNode ShouldHaveName(this XmlNode node, string expectedName)
    {
        Assert.Equal(expectedName, node.Name);
        return node;
    }
    
    public static XmlNode ShouldHaveValue(this XmlNode node, string expectedValue)
    {
        Assert.Equal(expectedValue, node.InnerText);
        return node;
    }
    
    public static XmlNode ShouldHaveValueMatching(this XmlNode node, string expectedValueRegex)
    {
        Assert.Matches(expectedValueRegex, node.InnerText);
        return node;
    }
    
    public static void ShouldHaveAttributes(this XmlNode node, params (string key, string value)[] expectedAttributes)
    {
        Assert.Equal(expectedAttributes.Length, node.Attributes?.Count ?? 0);

        var actualAttributes = node.Attributes!;
        foreach (var (expectedAttributeKey, expectedAttributeValue) in expectedAttributes)
        {
            Assert.NotNull(actualAttributes[expectedAttributeKey]);
            Assert.Equal(expectedAttributeValue, actualAttributes[expectedAttributeKey]!.InnerText);
        }
    }
    
    public static void ThatMatch(this XmlAttributeCollection? attributes, params (string key, string value)[] expectedAttributes)
    {
        Assert.Equal(expectedAttributes.Length, attributes?.Count ?? 0);

        var actualAttributes = attributes!;
        foreach (var (expectedAttributeKey, expectedAttributeValue) in expectedAttributes)
        {
            Assert.NotNull(actualAttributes[expectedAttributeKey]);
            Assert.Equal(expectedAttributeValue, actualAttributes[expectedAttributeKey]!.InnerText);
        }
    }
    
    public static XmlNode And(this XmlNode node)
    {
        // No-op - this is just for nicer readability
        return node;
    }

    public static void ChildrenMatching(this XmlNode node, params Action<XmlNode>[] childInspectors)
    {
        var referencesNodeChildren = node.Cast<XmlNode>();

        Assert.Collection(referencesNodeChildren, childInspectors);
    }
    
    public static XmlNode With(this XmlNode node)
    {
        // No-op - this is just for nicer readability
        return node;
    }
    public static XmlNode Which(this XmlNode node)
    {
        // No-op - this is just for nicer readability
        return node;
    }
}
