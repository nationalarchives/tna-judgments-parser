
using System.Linq;

using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    public class Metadata
    {

        public string Title { get; init; }

        public static Metadata Extract(Bill bill) {
            string title = Judgments.Util.Descendants<ShortTitle>(bill.CoverPage)
                .Select(title => IInline.ToString(title.Contents))
                .FirstOrDefault();
            return new() { Title = title };
        }

    }

}
