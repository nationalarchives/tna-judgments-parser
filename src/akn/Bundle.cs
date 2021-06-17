
using System.Collections.Generic;
using System.Xml;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

public interface IBundle {

    XmlDocument Judgment { get; }

    IEnumerable<IImage> Images { get; }

    void Close();

}

}
