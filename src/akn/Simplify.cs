
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using UK.Gov.Legislation.Judgments.AkomaNtoso;

namespace UK.Gov.NationalArchives.AkomaNtoso
{

    class Simplifier
    {

        public static void Simplify(XmlDocument doc)
        {
            new Simplifier(doc).VisitDocument();
        }

        readonly XmlDocument Document;
        readonly XmlNamespaceManager NamespaceManager;
        readonly Dictionary<string, Dictionary<string, string>> Styles;
        Dictionary<string, string> State;

        private Simplifier(XmlDocument doc)
        {
            Document = doc;
            NamespaceManager = new XmlNamespaceManager(doc.NameTable);
            NamespaceManager.AddNamespace("akn", Builder.ns);
            Styles = ParseStyles();
            State = new();
        }

        private Dictionary<string, Dictionary<string, string>> ParseStyles()
        {
            Dictionary<string, Dictionary<string, string>> css = new();
            XmlNode presentation = Document.SelectSingleNode("/akn:akomaNtoso/akn:*/akn:meta/akn:presentation", NamespaceManager);
            if (presentation is null)
                return css;
            foreach (var item in presentation.InnerText.Split('}'))
            {
                if (!item.Contains('{'))
                    continue;
                var split = item.Split('{', 2);

                var selector = string.Join(' ', split[0].Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Where(s => !s.StartsWith('#')));
                if (selector.StartsWith('.'))
                    selector = selector[1..];

                Dictionary<string, string> props = ParseProperties(split[1]);
                if (string.IsNullOrEmpty(selector))
                    State = props;
                else
                    css.Add(selector, props);
            }
            presentation.ParentNode.RemoveChild(presentation);
            return css;
        }

        private static Dictionary<string, string> ParseProperties(string properties)
        {
            Dictionary<string, string> props = new();
            foreach (var item in properties.Split(';'))
            {
                if (!item.Contains(':'))
                    continue;
                var split = item.Split(':', 2);
                var property = split[0].Trim();
                var value = split[1].Trim();
                props.Add(property, value);
            }
            return props;
        }

        private Dictionary<string, string> GetClassProperties(XmlElement e)
        {
            string cls = e.GetAttribute("class");
            if (string.IsNullOrEmpty(cls))
                return new();
            if (Styles.ContainsKey(cls))
                return Styles[cls];
            return new();
        }

        private static IDictionary<string, string> GetStyleProperties(XmlElement e)
        {
            string style = e.GetAttribute("style");
            if (style is null)
                return new Dictionary<string, string>();
            return ParseProperties(style);
        }

        private void VisitDocument()
        {
            VisitElement(Document.DocumentElement);
        }

        private void VisitNode(XmlNode node)
        {
            if (node.NodeType == XmlNodeType.Element)
                VisitElement(node as XmlElement);
            if (node.NodeType == XmlNodeType.Text)
                VisitText(node as XmlText);
        }

        private void VisitElement(XmlElement e)
        {
            Dictionary<string, string> copy = new(this.State);
            UpdateStateAndRemoveStyleAttributes(e);
            AddClassToPrefaceParagraph(e);
            e.ChildNodes.Cast<XmlNode>().ToList().ForEach(VisitNode);
            RemoveSpan(e);
            State = copy;
        }

        private void UpdateStateAndRemoveStyleAttributes(XmlElement e) {
            foreach (KeyValuePair<string, string> item in GetClassProperties(e))
                State[item.Key] = item.Value;
            foreach (KeyValuePair<string, string> item in GetStyleProperties(e))
                State[item.Key] = item.Value;
            e.RemoveAttribute("class");
            e.RemoveAttribute("style");
        }

        private void AddClassToPrefaceParagraph(XmlElement p) {
            if (p.ParentNode.LocalName != "preface")
                return;
            State.TryGetValue("text-align", out string align);
            if (align == "center" || align == "right")
                p.SetAttribute("class", align);
        }

        private static void RemoveSpan(XmlElement span) {
            if (span.LocalName != "span")
                return;
            List<XmlNode> children = span.ChildNodes.Cast<XmlNode>().ToList();
            foreach (XmlNode child in children)
            {
                span.RemoveChild(child);
                span.ParentNode.InsertAfter(child, span);
            }
            span.ParentNode.RemoveChild(span);
        }

        private void VisitText(XmlText text)
        {
            if (State.GetValueOrDefault("font-weight") == "bold")
            {
                var b = text.OwnerDocument.CreateElement("b", Builder.ns);
                text.ParentNode.ReplaceChild(b, text);
                b.AppendChild(text);
                State.Remove("font-weight");
                VisitText(text);
                State["font-weight"] = "bold";
                return;
            }
            if (State.GetValueOrDefault("font-style") == "italic")
            {
                var i = text.OwnerDocument.CreateElement("i", Builder.ns);
                text.ParentNode.ReplaceChild(i, text);
                i.AppendChild(text);
                State.Remove("font-style");
                VisitText(text);
                State["font-style"] = "italic";
                return;
            }
            if (State.GetValueOrDefault("text-decoration") == "underline")
            {
                var u = text.OwnerDocument.CreateElement("u", Builder.ns);
                text.ParentNode.ReplaceChild(u, text);
                u.AppendChild(text);
                State.Remove("text-decoration");
                VisitText(text);
                State["text-decoration"] = "underline";
                return;
            }
            if (State.GetValueOrDefault("text-decoration-line") == "underline")
            {
                var u = text.OwnerDocument.CreateElement("u", Builder.ns);
                text.ParentNode.ReplaceChild(u, text);
                u.AppendChild(text);
                State.Remove("text-decoration-line");
                VisitText(text);
                State["text-decoration-line"] = "underline";
                return;
            }
            if (State.GetValueOrDefault("text-transform") == "uppercase") {
                text.Value = text.Value.ToUpper();
                return;
            }
        }

    }

}