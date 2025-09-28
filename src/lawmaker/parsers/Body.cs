
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class LegislationParser
    {

        private readonly List<IDivision> body = [];

        private void ParseBody()
        {
            while (i < Document.Body.Count)
            {
                IDivision div = ParseNextBodyDivision();
                // We have encountered the conclusion so exit the while loop early
                if (div is UnknownLevel unknownLvl && ExplanatoryNote.IsHeading(langService, unknownLvl.Contents[0]))
                {
                    i -= 1;
                    break;
                }
    
                if (div is not null)
                    body.Add(div);
            }
        }

        // always leaves i in the right place; shouldn't return null, unless unhandled block type
        private IDivision ParseNextBodyDivision()
        {
            if (Match(LdappTableBlock.Parse) is LdappTableBlock tableBlock)
            {
                return new WDummyDivision(tableBlock);
            }
            HContainer hContainer = ParseLine();
            if (hContainer is not null)
            {
                return hContainer;
            }
            IBlock block = Current();
            if (block is NationalArchives.TableOfContents toc)
            {
                i += 1;
                return new WTableOfContents(toc.Contents);
            }
            Logger.LogCritical("unexpected block: {}", block.GetType().ToString());
            i += 1;
            return null;
        }

    }

}
