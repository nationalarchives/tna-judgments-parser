
using System.Collections.Generic;
using System.Linq;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Models;

namespace UK.Gov.Legislation.Common {

internal static class StatisticsCalculator {

    internal static DocumentStatistics Calculate(IDocument document) {
        int bodyParagraphs = CountBodyParagraphs(document);
        int scheduleParagraphs = CountAnnexParagraphs(document);
        int totalImages = document.Images?.Count() ?? 0;

        return new DocumentStatistics {
            BodyParagraphs = bodyParagraphs,
            ScheduleParagraphs = scheduleParagraphs,
            TotalParagraphs = bodyParagraphs + scheduleParagraphs,
            TotalImages = totalImages
        };
    }

    private static int CountBodyParagraphs(IDocument document) {
        if (document is IDividedDocument divided)
            return CountParagraphsInDivisions(divided.Body);
        if (document is IUndividedDocument undivided)
            return undivided.Body?.Count() ?? 0;
        return 0;
    }

    private static int CountParagraphsInDivisions(IEnumerable<IDivision> divisions) {
        if (divisions == null) return 0;
        int count = 0;
        foreach (var div in divisions) {
            if (div is IParagraph || div.Name == "paragraph")
                count++;
            else if (div is IBranch branch)
                count += CountParagraphsInDivisions(branch.Children);
        }
        return count;
    }

    private static int CountAnnexParagraphs(IDocument document) {
        if (document.Annexes == null) return 0;
        return document.Annexes.Sum(a => a.Contents?.Count() ?? 0);
    }

}

}
