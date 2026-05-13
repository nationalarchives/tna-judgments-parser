using System.Collections.Generic;
using System.Xml;

using UK.Gov.NationalArchives.AkomaNtoso;
using Builder = UK.Gov.Legislation.Judgments.AkomaNtoso.Builder;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Leg-specific AKN simplifier. Two presentational behaviours that the
/// leg HTML pipeline needs but other consumers don't:
///
///   1. <c>style</c> / <c>class</c> attributes are preserved on
///      <c>&lt;td&gt;</c> / <c>&lt;th&gt;</c> — leg tables carry
///      cell-level background colour and border styling that must
///      round-trip into HTML.
///   2. Inline runs with a non-default <c>color</c> or
///      <c>background-color</c> are wrapped in a styled <c>&lt;span&gt;</c>
///      so the visible colour band survives the simplifier's
///      bold/italic/underline rewrites (RAG-style "Green / Amber / Red"
///      labels in IA cover sheets, etc.).
///
/// Used by <see cref="BaseHelper"/> for every leg doc type. Other
/// consumers (judgment parser doesn't run the simplifier; lawmaker has
/// its own shadow-pattern subclass) use the base
/// <see cref="Simplifier"/> directly.
/// </summary>
internal class LegSimplifier : Simplifier {

    private LegSimplifier(XmlDocument doc) : base(doc) { }

    internal static new void Simplify(XmlDocument doc) {
        new LegSimplifier(doc).VisitDocument();
    }

    protected override bool ShouldStripStyleAttributes(XmlElement e) {
        return e.LocalName != "td" && e.LocalName != "th";
    }

    protected override void VisitText(XmlText text) {
        string bg = State.GetValueOrDefault("background-color");
        if (!string.IsNullOrEmpty(bg) && bg != "auto" && bg != "transparent" && bg != "initial" && bg != "white" && bg != "#ffffff" && bg != "#FFFFFF") {
            var span = text.OwnerDocument.CreateElement("span", Builder.ns);
            span.SetAttribute("style", "background-color:" + bg);
            text.ParentNode.ReplaceChild(span, text);
            span.AppendChild(text);
            State.Remove("background-color");
            VisitText(text);
            State["background-color"] = bg;
            return;
        }
        string fg = State.GetValueOrDefault("color");
        if (!string.IsNullOrEmpty(fg) && fg != "auto" && fg != "initial" && fg != "inherit") {
            var span = text.OwnerDocument.CreateElement("span", Builder.ns);
            span.SetAttribute("style", "color:" + fg);
            text.ParentNode.ReplaceChild(span, text);
            span.AppendChild(text);
            State.Remove("color");
            VisitText(text);
            State["color"] = fg;
            return;
        }
        base.VisitText(text);
    }

}

}
