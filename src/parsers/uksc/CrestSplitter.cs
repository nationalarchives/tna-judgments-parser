
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parse
{
    class CrestSplitter : Enricher
    {

        internal override IEnumerable<IBlock> Enrich(IEnumerable<IBlock> blocks)
        {
            IEnumerator<IBlock> x = blocks.GetEnumerator();
            if (!x.MoveNext())
                return blocks;
            List<IBlock> enriched = [];
            if (x.Current is IRestriction)
            {
                enriched.Add(x.Current);
                if (!x.MoveNext())
                    return blocks;
            }
            if (x.Current is not WLine line)
                return blocks;
            if (line.Contents.FirstOrDefault() is not IImageRef image)
                return blocks;
            WLine one = WLine.Make(line, [image]);
            enriched.Add(one);
            if (line.Contents.Skip(1).Any())
            {
                WLine two = WLine.Make(line, line.Contents.Skip(1));
                enriched.Add(two);
            }
            while (x.MoveNext())
                enriched.Add(x.Current);
            return enriched;
        }

        protected override IEnumerable<IInline> Enrich(IEnumerable<IInline> line)
        {
            throw new System.NotImplementedException();
        }
    }

}
