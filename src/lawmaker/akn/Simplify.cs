
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
            if (e.HasAttribute("keep"))
            {
                // workaround becuase there ARE some span elements we don't
                // want to delete
                e.RemoveAttribute("keep");
            } else
            {
                RemoveSpan(e); // why??
            }
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
            // Do nothing for the moment
        }

    }

}