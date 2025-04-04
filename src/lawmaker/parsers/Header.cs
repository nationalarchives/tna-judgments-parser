
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private readonly List<IBlock> coverPage = [];

        private readonly List<IBlock> preface = [];

        private readonly List<IBlock> preamble = [];

        private void ParseAndEnrichHeader()
        {
            ParseHeader();
            EnrichHeader();
        }

        private void ParseHeader()
        {
            bool foundPreface = false;
            while (i < Document.Body.Count - 10)
            {
                List<IBlock> blocks = [];
                blocks.Add(Document.Body[i].Block);
                blocks.Add(Document.Body[i + 1].Block);
                blocks.Add(Document.Body[i + 2].Block);

                if (!foundPreface && blocks.All(b => b is WLine))
                {
                    IEnumerable<string> textContent = blocks
                        .Select(b => (b as WLine).TextContent)
                        .Select(s => Regex.Replace(s, @"\s", ""));

                    string longTitle = string.Join(" ", textContent).ToUpper();
                    if (longTitle == "A BILL TO")
                    {
                        preface.AddRange(blocks);
                        i += 3;
                        foundPreface = true;
                        continue;
                    }
                }
                if (foundPreface && blocks[0] is WLine line)
                {
                    string enactingText = Regex.Replace(line.TextContent, @"\s", "").ToUpper();
                    if (enactingText.StartsWith("BEITENACTEDBY"))
                    {
                        preamble.Add(blocks[0]);
                        i += 1;
                        return;
                    }
                }
                if (foundPreface)
                    preface.Add(blocks[0]);
                else
                    coverPage.Add(blocks[0]);
                i += 1;
            }
            coverPage.Clear();
            preface.Clear();
            preamble.Clear();
            i = 0;
        }

        private void EnrichHeader()
        {
            EnrichCoverPage();
        }

        private void EnrichCoverPage()
        {
            if (coverPage.Count == 0)
                return;
            if (coverPage[0] is not WLine first)
                return;
            if (!first.NormalizedContent.EndsWith(" Bill"))
                return;
            ShortTitle shortTitle = new() { Contents = [.. first.Contents] };
            WLine replacement = WLine.Make(first, [shortTitle]);
            coverPage.RemoveAt(0);
            coverPage.Insert(0, replacement);
        }

    }

}
