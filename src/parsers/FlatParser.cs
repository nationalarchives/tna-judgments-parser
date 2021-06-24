
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

    class FlatParser : AbstractParser {

        public FlatParser(WordprocessingDocument doc) : base(doc) { }

        protected override List<IBlock> Header() {
            return null;
        }
    }

}
