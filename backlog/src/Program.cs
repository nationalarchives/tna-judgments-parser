
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.S3.Model;

using Backlog.Src.Batch.One;

namespace Backlog.Src
{

    class Program
    {

        static int Main(string[] args)
        {
            uint id = 2;
            bool autoPublish = true;

            DotNetEnv.Env.Load();  // required for bucket name

            Helper helper = new()
            {
                PathToCourtMetadataFile = Environment.GetEnvironmentVariable("COURT_METADATA_PATH") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "court_metadata.csv"),
                PathDoDataFolder = Environment.GetEnvironmentVariable("DATA_FOLDER_PATH") ?? AppDomain.CurrentDomain.BaseDirectory
            };
            string trackerPath = Environment.GetEnvironmentVariable("TRACKER_PATH") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploaded-production.csv");
            Tracker tracker = new Tracker(trackerPath);

            List<Metadata.Line> lines = helper.FindLines(id);

            foreach (var line in lines)
            {
                if (tracker.WasDone(line)) {
                    System.Console.WriteLine("skipping " + line.id);
                    continue;
                }

                Bundle bundle = helper.GenerateBundle(line, autoPublish);

                string outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? AppDomain.CurrentDomain.BaseDirectory;
                string output = Path.Combine(outputPath, bundle.Uuid + ".tar.gz");
                System.IO.File.WriteAllBytes(output, bundle.TarGz);

                System.Console.WriteLine(bundle.Uuid + ".tar.gz");
                System.Console.WriteLine(System.DateTime.Now);

                Task<PutObjectResponse> task = Bucket.UploadBundle(bundle.Uuid + ".tar.gz", bundle.TarGz);
                var x = task.Result;

                tracker.MarkDone(line, bundle.Uuid);

                System.Console.WriteLine("success");
                System.Console.WriteLine(bundle.Uuid + ".tar.gz");
                System.Console.WriteLine(System.DateTime.Now);

            }

            return 0;
        }

    }

}
