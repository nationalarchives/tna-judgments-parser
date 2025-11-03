
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using DocumentFormat.OpenXml.Office2016.Excel;

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
            // Inline elements with styles applied (i.e. italics) are wrapped in spans.
            // For inline styles which have dedicated elements (i, b, u, sub, sup) we 
            // convert remove the span and replace it with the corresponding element.
            // There are some spans (marked with 'keep') which we do NOT want to delete.
            e.ChildNodes.Cast<XmlNode>().ToList().ForEach(VisitNode);
            if (!e.HasAttribute("keep"))
                RemoveSpan(e);
            else
                e.RemoveAttribute("keep");
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
            if (!ShouldInsertStyleTags(text))
                return;
            if (ApplyStylingIfPresent(text, "font-weight", "bold", "b")) 
                return;
            if (ApplyStylingIfPresent(text, "font-style", "italic", "i")) 
                return;
            if (ApplyStylingIfPresent(text, "text-decoration", "underline", "u")) 
                return;
            if (ApplyStylingIfPresent(text, "text-decoration-line", "underline", "u")) 
                return;
            if (ApplyStylingIfPresent(text, "vertical-align", "super", "sup")) 
                return;
            if (ApplyStylingIfPresent(text, "vertical-align", "sub", "sub")) 
                return;
        }

        private static bool ShouldInsertStyleTags(XmlText text)
        {
            List<string> allowedParents = ["span", "b", "i", "u", "sup", "sub"];
            List<string> checkGrandparents = ["p", "heading", "block", "num", "tocItem"];
            string parentName = text.ParentNode.LocalName;
            XmlNode grandparent = text.ParentNode.ParentNode;
            // Don't need to insert style tags for whitespace
            if (string.IsNullOrWhiteSpace(text.Value))
                return false;
            if (!allowedParents.Contains(parentName))
                return false;
            // The parent span is a lone child meaning the current XmlText is the entire line so don't insert inline style tags
            if (parentName.Equals("span") && checkGrandparents.Contains(grandparent.LocalName) && grandparent.ChildNodes.Count < 2)
                return false;
            return true;
        }
        
        private bool ApplyStylingIfPresent(XmlText text, string attribute, string style, string tag)
        {
            if (State.GetValueOrDefault(attribute) == style)
            {
                var element = text.OwnerDocument.CreateElement(tag, Builder.ns);
                text.ParentNode.ReplaceChild(element, text);
                element.AppendChild(text);
                State.Remove(attribute);
                VisitText(text);
                State[attribute] = style;
                return true;
            }
            return false;
        }

    }

}