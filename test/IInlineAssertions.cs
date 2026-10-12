using DocumentFormat.OpenXml.Wordprocessing;

using Shouldly;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace test;

public static class IInlineAssertions
{
    internal static T ShouldMatch<T>(this IInline inline, string expectedText, RunProperties expected) where T : WText
    {
        var typed = inline.ShouldBeOfType<T>();

        typed.Text.ShouldBe(expectedText);
        typed.properties.ShouldBeSameAs(expected);

        return typed;
    }
}
