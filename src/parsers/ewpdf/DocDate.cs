using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UK.Gov.Legislation.Judgments.Parse {

class DocDatePDF : DocDate {


    override protected WLine Enrich2OrDefault(WLine line) {
        WLine super = base.Enrich2OrDefault(line);
        if (super is not null)
            return super;
        IInline first = line.Contents.First();
        IInline second = line.Contents.ElementAt(1);
        if (first is not WText fText1)
            return null;
        if (second is not WText fText2)
            return null;
        if (fText2.Text != "  Before: ")
            return null;
        List<IInline> enriched = EnrichText(fText1);
        if (enriched is null)
            return null;
        IEnumerable<IInline> contents = enriched.Append(new WLineBreak()).Append(fText2);
        return new WLine(line, contents);
    }

}

}
