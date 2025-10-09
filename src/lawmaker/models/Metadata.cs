
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Lawmaker
{

    public class Metadata
    {

        public string Title { get; init; }

        public static Metadata Extract(Document bill, ILogger logger) {
            string title = "";
            try
            {
                title = Judgments.Util.Descendants<ShortTitle>(bill.CoverPage)
                .Select(title => IInline.ToString(title.Contents))
                .FirstOrDefault();
            }
            catch (Exception e)
            {
                logger.LogError(e, "error converting EMF bitmap record");
            }
            return new() { Title = title };
        }

    }

}
