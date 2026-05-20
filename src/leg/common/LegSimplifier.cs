using System.Collections.Generic;
using System.Xml;

using UK.Gov.NationalArchives.AkomaNtoso;
using Builder = UK.Gov.Legislation.Judgments.AkomaNtoso.Builder;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Leg-specific AKN simplifier. Two behaviours leg HTML pipelines need:
/// preserve cell-level style on <c>&lt;td&gt;</c> / <c>&lt;th&gt;</c>
/// (so cell shading survives to HTML), and wrap inline runs with a
/// non-default <c>color</c> / <c>background-color</c> in a styled
/// <c>&lt;span&gt;</c> (so the colour band survives the bold/italic
/// rewrites — RAG labels in IA cover sheets).
/// </summary>
internal class LegSimplifier : Simplifier {

    private LegSimplifier(XmlDocument doc) : base(doc) { }

    internal static new void Simplify(XmlDocument doc) {
        new LegSimplifier(doc).VisitDocument();
    }

    protected override bool ShouldStripStyleAttributes(XmlElement e) =>
        e.LocalName != "td" && e.LocalName != "th";

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
