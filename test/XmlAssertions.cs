#nullable enable

using System;
using System.Linq;
using System.Xml;

using Xunit;
using Xunit.Sdk;

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
        var (isMatch, message) = node.IsMatch(expectedName, expectedValue, expectedAttributes);
        Assert.True(isMatch, message);
    }

    private static (bool isMatch, string message) IsMatch(this XmlNode node, string expectedName, string expectedValue,
        params (string key, string value)[] expectedAttributes)
    {
        if (expectedName != node.Name)
            return (false, $"Expected Name to be {expectedName} but it was {node.Name}");

        if (expectedValue != node.InnerText)
            return (false, $"Expected InnerText to be \"{expectedValue}\" but it was \"{node.InnerText}\"");

        var actualAttributeCount = node.Attributes?.Count ?? 0;
        if (expectedAttributes.Length != actualAttributeCount)
            return (false, $"Expected attribute count to be {expectedAttributes.Length} but it was {actualAttributeCount}");
        
        var actualAttributes = node.Attributes!;
        foreach (var (expectedAttributeKey, expectedAttributeValue) in expectedAttributes)
        {
            var actualAttributeValue = actualAttributes[expectedAttributeKey]?.InnerText;
            if (actualAttributeValue is null)
                return (false, $"Could not find attribute {expectedAttributeKey} on {expectedName}");
            if (expectedAttributeValue != actualAttributeValue)
                return (false, $"Expected attribute {expectedAttributeKey} to have value \"{expectedAttributeValue}\" but it was \"{actualAttributeValue}\"");
        }

        return (true, "");
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
        var referencesNodeChildren = node.Cast<XmlNode>().ToArray();

        var expectedNumberOfChildren = childInspectors.Length;
        var actualNumberOfChildren = referencesNodeChildren.Length;
        Assert.True(actualNumberOfChildren == expectedNumberOfChildren, $"Expected there to be {expectedNumberOfChildren} children of node <{node.Name}> but found {actualNumberOfChildren}");

        var unpassedInspectors = childInspectors.ToList();
        
        foreach (var child in referencesNodeChildren)
        {
            var hasPassedInspection = false;
            
            foreach (var inspector in unpassedInspectors)
            {
                try
                {
                    inspector(child);
                    hasPassedInspection = true;
                    unpassedInspectors.Remove(inspector);
                    break;
                }
                catch (XunitException)
                {
                    // This inspector failed, try the next one
                }
            }

            Assert.True(hasPassedInspection, $"""Found unexpected child node <{child.Name}> with value "{child.InnerText}" in <{node.Name}>""");
        }

        Assert.True(unpassedInspectors.Count == 0, $"{unpassedInspectors.Count} inspectors were not satisfied for <{node.Name}>");
    }
    
    public static XmlNode HasChildMatching(this XmlNode node, string expectedName, string expectedValue)
    {
        var childNodes = node.Cast<XmlNode>();
        
        if (!childNodes.Any(child => child.IsMatch(expectedName, expectedValue).isMatch))
            Assert.Fail($"Could not find a child of {node.Name} with name {expectedName} and value \"{expectedValue}\"");

        return node;
    }
    
    public static XmlNode DoesNotHaveChildWithName(this XmlNode node, string name)
    {
        var childNodes = node.Cast<XmlNode>();

        if (childNodes.Any(child => string.Equals(child.Name, name, StringComparison.InvariantCultureIgnoreCase)))
            Assert.Fail($"Was not expecting to find a child of {node.Name} with name {name}");

        return node;
    }

    #region Readability No Ops
    
    public static XmlNode And(this XmlNode node) => node;
    public static XmlNode Should(this XmlNode node) => node;
    public static XmlNode Which(this XmlNode node) => node;

    #endregion

}
