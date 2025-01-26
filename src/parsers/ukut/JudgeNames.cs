
using System;
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT
{
    /// <summary>
    /// Enriches lines of text by identifying and annotating judge names
    /// </summary>
    class JudgeNames : Enricher
    {

        internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
        {
            List<IBlock> enriched = [];
            int i = 0;
            bool foundStart = false;
            while (!foundStart && i < blocks.Count())
            {
                IBlock block = blocks.ElementAt(i);
                if (IsDecidedBy(block))
                    foundStart = true;
                enriched.Add(block);
                i += 1;
            }
            if (!foundStart)
                return blocks;
            if (i == blocks.Count())
                return blocks;
            IBlock next = blocks.ElementAt(i);
            if (next is not WLine line)
                return blocks;
            if (!IsJudgeName(line))
                return blocks;
            WLine rich = WLine.Make(line, line.Contents.Cast<WText>().Select(t => new WJudge(t)));
            enriched.Add(rich);
            i += 1;
            enriched.AddRange(blocks.Skip(i));
            return enriched;
        }

        private static bool IsDecidedBy(IBlock block)
        {
            if (block is not WLine line)
                return false;
            if (line.Contents.Count() != 1)
                return false;
            if (line.Contents.First() is not WText text)
                return false;
            if (text.Text.TrimEnd(':') == "Before")
                return true;
            return text.Text.TrimEnd(':') == "Decided by";
        }

        private static bool IsJudgeName(WLine line)
        {
            return line.NormalizedContent.StartsWith("TRIBUNAL JUDGE", StringComparison.InvariantCultureIgnoreCase);
        }

        protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
        {
            throw new NotImplementedException();
        }
    }

}
