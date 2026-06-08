
using System.Collections.Generic;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    public class Bundle {

        public string Xml { get; internal init; }

        public IEnumerable<IImage> Images { get; internal init; }

    }

}
