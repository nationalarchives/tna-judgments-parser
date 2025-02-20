
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private readonly List<IDivision> body = [];
        private bool isInSchedule = false;

        private void ParseBody()
        {
            while (i < Document.Body.Count)
            {
                IDivision div = ParseNextBodyDivision();
                if (div is not null)
                    body.Add(div);
            }
        }

        // always leaves i in the right place; shouldn't return null, unless unhandled block type
        private IDivision ParseNextBodyDivision()
        {
            HContainer hContainer = ParseLine();
            if (hContainer is not null)
            {
                return hContainer;
            }
            IBlock block = Current();
            if (block is WTable table)
            {
                i += 1;
                return new WDummyDivision(table);
            }
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
