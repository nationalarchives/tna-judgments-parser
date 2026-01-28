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
        if (niHeader.Preamble is not Preamble preamble) return null;
        List<IBlock?>? preambleContents = preamble.Blocks?.ToList();
        if (preambleContents is null) return niHeader;
        EnrichPreamble(preambleContents);
        return niHeader with {
            Preamble = preamble with { Blocks = preambleContents },
        };
    }

    public UKHeader? VisitSC(UKHeader? scHeader)
    {
        if (scHeader is null) return null;
        if (scHeader.Preamble is not Preamble preamble) return null;
        List<IBlock?>? preambleContents = preamble.Blocks?.ToList();
        if (preambleContents is null) return scHeader;
        EnrichPreamble(preambleContents);
        return scHeader with {
            Preamble = preamble with { Blocks = preambleContents },
        };
    }

    public SPHeader? VisitSP(SPHeader? spHeader)
    {
        // SP bills don't have footnotes to enrich in the header
        return spHeader;
    }

    public UKHeader? VisitUK(UKHeader? ukHeader)
    {
        if (ukHeader is null) return null;
        if (ukHeader.Preamble is not Preamble preamble) return null;
        List<IBlock?>? preambleContents = preamble.Blocks?.ToList();
        if (preambleContents is null) return ukHeader;
        EnrichPreamble(preambleContents);
        return ukHeader with {
            Preamble = preamble with { Blocks = preambleContents },
        };
    }

    public CMHeader? VisitCM(CMHeader? cmHeader)
    {
        // Church measures don't have footnotes to enrich in the header
        return cmHeader;
    }
}