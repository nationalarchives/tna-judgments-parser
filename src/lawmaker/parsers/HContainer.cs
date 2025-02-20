
using System.Collections.Generic;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.Legislation.Lawmaker
{

    public partial class BillParser
    {

        private readonly Dictionary<(string, int, int), (object Result, int NextPosition)> memo = [];

        // call only if line is Current()
        private T ParseAndMemoize<T>(WLine line, string name, System.Func<WLine, T> parseFunction) {
            var key = (name, i, quoteDepth);
            if (memo.TryGetValue(key, out var cached)) {
                i = cached.NextPosition;
                return (T)cached.Result;
            }
            int save = i;
            T result = parseFunction(line);
            if (result is null)
                i = save;
            memo[key] = (result, i);
            return result;
        }

        private HContainer ParseLine()
        {
            if (Current() is not WLine line)
                return null;

            HContainer hContainer;

            hContainer = ParseAndMemoize(line, "Schedules", ParseSchedules);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Schedule", ParseSchedule);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Part", ParsePart);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "CrossHeading", ParseCrossheading);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Prov1", ParseProv1);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Prov2", ParseProv2);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Para1", ParsePara1);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "Para2", ParsePara2);
            if (hContainer != null)
                return hContainer;

            hContainer = ParseAndMemoize(line, "UnnumberedParagraph", ParseUnnumberedParagraph);
            if (hContainer != null)
                return hContainer;

            i += 1;
            if (line is WOldNumberedParagraph np)
                return new UnknownLevel() { Number = np.Number, Contents = [WLine.RemoveNumber(np)] };
            else
                return new UnknownLevel() { Contents = [line] };
        }


        /* helper functions for content, intro and wrapUp */

        private void AddFollowingToContent(WLine leader, List<IBlock> container)
        {
            while (i < Document.Body.Count)
            {
                IBlock current = Current();
                if (Current() is not WLine line)
                {
                    i += 1;
                    container.Add(current);
                    continue;
                }
                QuotedStructure qs = ParseQuotedStructure(line);
                if (qs is not null)
                {
                    container.Add(qs);
                    continue;
                }
                if (line is WOldNumberedParagraph)
                    break;
                if (!IsLeftAligned(line))
                    break;
                if (LineIsIndentedLessThan(line, leader))
                    break;
                int save = i;
                HContainer test = ParseProv1(line);
                i = save;
                if (test is not null)
                    break;
                i += 1;
                container.Add(line);
            }
        }

        private void AddFollowingToIntroOrWrapUp(WLine leader, List<IBlock> container)
        {
            while (i < Document.Body.Count)
            {
                if (Current() is not WLine line)
                    break;
                if (line is WOldNumberedParagraph)
                    break;
                if (!IsLeftAligned(line))
                    break;
                if (LineIsIndentedLessThan(line, leader))
                    break;
                int save = i;
                HContainer test = ParseProv1(line);
                i = save;
                if (test is not null)
                    break;
                i += 1;
                container.Add(line);
            }
        }

    }

}
