
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.S3.Model;

using Backlog.Src.Batch.Three;

namespace Backlog.Src
{

    class Program
    {

        static int Main(string[] args)
        {
            uint id = 0;
            bool autoPublish = true;

            DotNetEnv.Env.Load();  // required for bucket name

            Helper helper = new()
            {
                PathToCourtMetadataFile = @"/Users/ahashemi/Documents/Projects/tna-judgments-parser/caselaw-parser-assets/TDR_Tribunal_Exports/TDR-2025-CQSX2025-04-03T14:55:20.397Z/tribunal-file-metadata.csv",
                PathDoDataFolder = @"/Users/ahashemi/Documents/Projects/tna-judgments-parser/caselaw-parser-assets/TDR_Tribunal_Exports/TDR-2025-CQSX2025-04-03T14:55:20.397Z"
            };
            Tracker tracker = new Tracker(@"/Users/ahashemi/Documents/Projects/tna-judgments-parser/caselaw-parser-assets/TDR_Tribunal_Exports/TDR-2025-CQSX2025-04-03T14:55:20.397Z/uploaded-staging.csv");

            // List<Metadata.Line> lines = helper.FindLines(id);
            List<Metadata.Line> lines = Metadata.Read(helper.PathToCourtMetadataFile);

            foreach (var line in lines)
            {
                if (tracker.WasDone(line))
                    continue;

                Bundle bundle = helper.GenerateBundle(line, autoPublish);

                string output = @"/Users/ahashemi/Documents/Projects/tna-judgments-parser/caselaw-parser-assets/TDR_Tribunal_Exports/TDR-2025-CQSX2025-04-03T14:55:20.397Z/" + bundle.Uuid + ".tar.gz";
                System.IO.File.WriteAllBytes(output, bundle.TarGz);

                Task<PutObjectResponse> task = Bucket.UploadBundle(bundle.Uuid + ".tar.gz", bundle.TarGz);
                var x = task.Result;

                tracker.MarkDone(line, bundle.Uuid);

                System.Console.WriteLine(bundle.Uuid + ".tar.gz");
                System.Console.WriteLine(System.DateTime.Now);

                // end after 1 file as a test
                return 0;

            }

            return 0;
        }

    }

}
