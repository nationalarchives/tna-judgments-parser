
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private readonly List<IDivision> body = [];

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
            IBlock block = Document.Body[i].Block;
            if (block is WLine line)
            {
                return ParseLine(line);
            }
            else if (block is WTable table)
            {
                i += 1;
                return new WDummyDivision(table);
            }
            else if (block is NationalArchives.TableOfContents toc)
            {
                i += 1;
                return new WTableOfContents(toc.Contents);
            }
            else
            {
                Logger.LogCritical("unexpected block: {}", block.GetType().ToString());
                i += 1;
                return null;
            }
        }

    }

}
