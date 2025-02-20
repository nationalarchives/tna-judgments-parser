
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
        public static void Simplify(XmlDocument doc, Dictionary<string, Dictionary<string, string>> styles)
        {
            new Simplifier(doc, styles).VisitDocument();
        }

        protected readonly XmlDocument Document;
        protected readonly XmlNamespaceManager NamespaceManager;
        protected readonly Dictionary<string, Dictionary<string, string>> Styles;
        protected Dictionary<string, string> State;

        protected Simplifier(XmlDocument doc)
        {
            Document = doc;
            NamespaceManager = new XmlNamespaceManager(doc.NameTable);
            NamespaceManager.AddNamespace("akn", Builder.ns);
            Styles = ParseStyles();
            State = new();
        }
        protected Simplifier(XmlDocument doc, Dictionary<string, Dictionary<string, string>> styles)
        {
            Document = doc;
            NamespaceManager = new XmlNamespaceManager(doc.NameTable);
            NamespaceManager.AddNamespace("akn", Builder.ns);
            Styles = CorrectStyles(styles);
            State = [];
        }

        protected Dictionary<string, Dictionary<string, string>> ParseStyles()
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

        protected Dictionary<string, Dictionary<string, string>> CorrectStyles(Dictionary<string, Dictionary<string, string>> styles)
        {
            Dictionary<string, Dictionary<string, string>> css = new();
            foreach (var item in styles)
            {
                var key = string.Join(' ', item.Key.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Where(s => !s.StartsWith('#')));
                if (key.StartsWith('.'))
                    key = key[1..];
                css.Add(key, item.Value);
            }
            return css;
        }

        protected static Dictionary<string, string> ParseProperties(string properties)
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

        protected Dictionary<string, string> GetClassProperties(XmlElement e)
        {
            string cls = e.GetAttribute("class");
            if (string.IsNullOrEmpty(cls))
                return new();
            if (Styles.ContainsKey(cls))
                return Styles[cls];
            return new();
        }

        protected static IDictionary<string, string> GetStyleProperties(XmlElement e)
        {
            string style = e.GetAttribute("style");
            if (style is null)
                return new Dictionary<string, string>();
            return ParseProperties(style);
        }

        protected void VisitDocument()
        {
            VisitElement(Document.DocumentElement);
        }

        protected void VisitNode(XmlNode node)
        {
            if (node.NodeType == XmlNodeType.Element)
                VisitElement(node as XmlElement);
            if (node.NodeType == XmlNodeType.Text)
                VisitText(node as XmlText);
        }

        protected void VisitElement(XmlElement e)
        {
            Dictionary<string, string> copy = new(this.State);
            UpdateStateAndRemoveStyleAttributes(e);
            AddClassToPrefaceOrAttachmentParagraph(e);
            e.ChildNodes.Cast<XmlNode>().ToList().ForEach(VisitNode);
            RemoveSpan(e);
            State = copy;
        }

        protected void UpdateStateAndRemoveStyleAttributes(XmlElement e)
        {
            foreach (KeyValuePair<string, string> item in GetClassProperties(e))
                State[item.Key] = item.Value;
            foreach (KeyValuePair<string, string> item in GetStyleProperties(e))
                State[item.Key] = item.Value;
            e.RemoveAttribute("class", "");
            e.RemoveAttribute("style", "");
            // replace "class" and "style" attributes with those in another namespace
            var toCopy = new List<XmlAttribute>();
            foreach (XmlAttribute attr in e.Attributes)
            {
                // if (string.IsNullOrEmpty(attr.NamespaceURI))
                //     continue;
                if (attr.LocalName == "class" || attr.LocalName == "style")
                    toCopy.Add(attr);
            }
            foreach (XmlAttribute attr in toCopy)
            {
                e.RemoveAttributeNode(attr);
                e.SetAttribute(attr.LocalName, "", attr.Value);
            }
        }

        protected static bool IsPrefaceParagraph(XmlElement p)
        {
            if (p.LocalName != "p")
                return false;
            return p.ParentNode.LocalName == "preface";
        }
        protected static bool IsAttachmentParagraph(XmlElement p)
        {
            if (p.LocalName != "p")
                return false;
            if (p.ParentNode.LocalName != "mainBody")
                return false;
            if (p.ParentNode.ParentNode.LocalName != "doc")
                return false;
            if (p.ParentNode.ParentNode.ParentNode.LocalName != "attachment")
                return false;
            return true;
        }
        protected void AddClassToPrefaceOrAttachmentParagraph(XmlElement p)
        {
            if (!IsPrefaceParagraph(p) && !IsAttachmentParagraph(p))
                return;
            State.TryGetValue("text-align", out string align);
            if (align == "center" || align == "right")
                p.SetAttribute("class", align);
        }

        protected static void RemoveSpan(XmlElement span)
        {
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

        protected void VisitText(XmlText text)
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
            string decor = State.GetValueOrDefault("text-decoration-line");
            if (decor == "underline")
            {
                var u = text.OwnerDocument.CreateElement("u", Builder.ns);
                text.ParentNode.ReplaceChild(u, text);
                u.AppendChild(text);
                State.Remove("text-decoration-line");
                VisitText(text);
                State["text-decoration-line"] = "underline";
                return;
            }
            // if (decor is not null && decor.StartsWith("underline"))
            // {
            //     var u = text.OwnerDocument.CreateElement("u", Builder.ns);
            //     text.ParentNode.ReplaceChild(u, text);
            //     u.AppendChild(text);
            //     var remaining = decor[9..].TrimEnd();
            //     if (string.IsNullOrEmpty(remaining))
            //         State.Remove("text-decoration-line");
            //     else
            //         State["text-decoration-line"] = remaining;
            //     VisitText(text);
            //     State["text-decoration-line"] = decor;
            //     return;
            // }
            if (State.GetValueOrDefault("text-transform") == "uppercase")
            {
                text.Value = text.Value.ToUpper();
                return;
            }
        }

    }

}