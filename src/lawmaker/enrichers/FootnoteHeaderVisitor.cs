#nullable enable
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Lawmaker.Headers;

namespace UK.Gov.Legislation.Lawmaker;
class FootnoteHeaderVisitor : IHeaderVisitor
{
    public required FootnoteEnricher FootnoteEnricher { get; init; }

    private void EnrichPreamble(List<IBlock?>? preamble)
    {
        if (preamble is null)
        {
            return;
        }
        FootnoteEnricher.EnrichBlocks(preamble);
    }

    public NIHeader? VisitNI(NIHeader? niHeader)
    {
        if (niHeader is null) return null;
        List<IBlock?>? preamble = niHeader.Preamble?.Blocks?.ToList();
        if (preamble is null) return niHeader;
        EnrichPreamble(preamble);
        return niHeader with {
            Preamble = new Headers.Preamble(preamble),
        };
    }

    public UKHeader? VisitSC(UKHeader? scHeader)
    {
        // Not enriching SC headers yet, but would be something like the below when we do:
        /*
        if (scHeader is null) return null;
        List<IBlock?>? preamble = scHeader.Preamble?.Blocks?.ToList();
        if (preamble is null) return scHeader;
        EnrichPreamble(preamble);
        return scHeader with {
            Preamble = new Headers.Preamble(preamble),
        };
        */
        return scHeader;
    }

    public SPHeader? VisitSP(SPHeader? spHeader)
    {
        // SP bills don't have footnotes to enrich in the header
        return spHeader;
    }

    public UKHeader? VisitUK(UKHeader? ukHeader)
    {
        // Not enriching UK headers yet, but would be something like the below when we do:
        /*
        if (ukHeader is null) return null;
        List<IBlock?>? preamble = ukHeader.Preamble?.Blocks?.ToList();
        if (preamble == null) return ukHeader;
        EnrichPreamble(preamble);
        return ukHeader with {
            Preamble = new Headers.Preamble(preamble),
        };
        */
        return ukHeader;
    }

    public CMHeader? VisitCM(CMHeader? cmHeader)
    {
        // Church measures don't have footnotes to enrich in the header
        return cmHeader;
    }
}