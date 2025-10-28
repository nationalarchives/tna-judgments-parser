
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace UK.Gov.Legislation.Lawmaker
{

    class Simplifier : NationalArchives.AkomaNtoso.Simplifier
    {

        protected Simplifier(XmlDocument doc) : base(doc)
        {
        }
        protected Simplifier(XmlDocument doc, Dictionary<string, Dictionary<string, string>> styles) : base(doc, styles)
        {
        }

        public static new void Simplify(XmlDocument doc)
        {
            new Simplifier(doc).VisitDocument();
        }
        public static new void Simplify(XmlDocument doc, Dictionary<string, Dictionary<string, string>> styles)
        {
            new Simplifier(doc, styles).VisitDocument();
        }

        protected new void VisitDocument()
        {
            VisitElement(Document.DocumentElement);
        }

        private new void VisitElement(XmlElement e)
        {
            Dictionary<string, string> copy = new(this.State);
            UpdateStateAndRemoveStyleAttributes(e);
            e.ChildNodes.Cast<XmlNode>().ToList().ForEach(VisitNode);
            RemoveSpan(e);
            State = copy;
        }

        protected new void VisitNode(XmlNode node)
        {
            if (node.NodeType == XmlNodeType.Element)
                VisitElement(node as XmlElement);
            if (node.NodeType == XmlNodeType.Text)
                VisitText(node as XmlText);
        }

        private new void VisitText(XmlText text)
        {
            List<string> allowedParents = ["span", "b", "i", "u", "sup", "sub"];
            List<string> checkGrandparents = ["p", "heading", "block", "num", "tocItem"];
            string parentName = text.ParentNode.LocalName;
            XmlNode grandparent = text.ParentNode.ParentNode;
            // Don't need to insert style tags for whitespace
            if (string.IsNullOrWhiteSpace(text.Value))
                return;
            if (!allowedParents.Contains(parentName))
                return;
            // The parent span is a lone child meaning the current XmlText is the entire line so don't insert inline style tags
            if (parentName.Equals("span") && checkGrandparents.Contains(grandparent.LocalName) && grandparent.ChildNodes.Count < 2)
                return;
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
            if (State.GetValueOrDefault("vertical-align") == "super")
            {
                var sup = text.OwnerDocument.CreateElement("sup", Builder.ns);
                text.ParentNode.ReplaceChild(sup, text);
                sup.AppendChild(text);
                State.Remove("vertical-align");
                VisitText(text);
                State["vertical-align"] = "super";
                return;
            }
            if (State.GetValueOrDefault("vertical-align") == "sub")
            {
                var sub = text.OwnerDocument.CreateElement("sub", Builder.ns);
                text.ParentNode.ReplaceChild(sub, text);
                sub.AppendChild(text);
                State.Remove("vertical-align");
                VisitText(text);
                State["vertical-align"] = "sub";
                return;
            }
        }

    }

}