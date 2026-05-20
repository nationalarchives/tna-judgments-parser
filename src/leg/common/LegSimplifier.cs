using System.Collections.Generic;
using System.Xml;

using UK.Gov.NationalArchives.AkomaNtoso;
using Builder = UK.Gov.Legislation.Judgments.AkomaNtoso.Builder;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// Leg-specific AKN simplifier. Wraps inline runs that have a non-default
/// <c>color</c> or <c>background-color</c> in a styled <c>&lt;span&gt;</c>
/// so the visible colour band survives the simplifier's
/// bold/italic/underline rewrites (RAG-style "Green / Amber / Red" labels
/// in IA cover sheets, etc.).
///
/// Used by <see cref="BaseHelper"/> for every leg doc type. The base
/// <see cref="Simplifier"/> handles td/th cell-style preservation for
/// every consumer (leg + lawmaker) — leg doesn't need to override that.
/// </summary>
internal class LegSimplifier : Simplifier {

    private LegSimplifier(XmlDocument doc) : base(doc) { }

    internal static new void Simplify(XmlDocument doc) {
        new LegSimplifier(doc).VisitDocument();
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
