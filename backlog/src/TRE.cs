
namespace Backlog.Src
{

    class TRE
    {

        internal class Metadata
        {

            public string Reference { get; set; }

            public Payload Payload { get; set; }

        }

        internal class Payload
        {

            public string Filename { get; set; }

            public string Xml { get; set; } = "judgment.xml";

            public string Metadata { get; set; } = "bulk-metadata.json";

            public string[] Images { get; set; } = [];

            public string Log { get; set; } = "parser.log";

        }

    }

}
