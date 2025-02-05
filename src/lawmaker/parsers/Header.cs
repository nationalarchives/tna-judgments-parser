
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private readonly List<IBlock> coverPage = [];

        private readonly List<IBlock> preface = [];

        private readonly List<IBlock> preamble = [];

        private void ParseHeader() {
            bool foundPreface = false;
            while (i < Document.Body.Count - 10) {
                var block1 = Document.Body[i].Block;
                var block2 = Document.Body[i + 1].Block;
                var block3 = Document.Body[i + 2].Block;
                if (!foundPreface && block1 is WLine line1 && block2 is WLine line2 && block3 is WLine line3) {
                    if (line1.TextContent == "A" && line2.TextContent == "Bill" && line3.TextContent == "to") {
                        preface.Add(block1);
                        preface.Add(block2);
                        preface.Add(block3);
                        i += 3;
                        foundPreface = true;
                        continue;
                    }
                }
                if (foundPreface && block1 is WLine line && line.TextContent.StartsWith("BE IT ENACTED by")) {
                    preamble.Add(block1);
                    i += 1;
                    return;
                }
                if (foundPreface) {
                    preface.Add(block1);
                } else {
                    coverPage.Add(block1);
                }
                i += 1;
            }
            coverPage.Clear();
            preface.Clear();
            preamble.Clear();
            i = 0;
        }

    }

}
