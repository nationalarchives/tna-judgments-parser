
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
            TestTracker(64);
            return 0;
        }

        static void OneLocal(uint id, bool autoPublish = false)
        {
            Helper helper = new()
            {
                PathToCourtMetadataFile = @"C:\Users\Administrator\TDR-2025-CNS6\court_metadata.csv",
                PathDoDataFolder = @"C:\Users\Administrator\TDR-2025-CNS6\"
            };
            List<Metadata.Line> lines = helper.FindLines(id);
            foreach (var line in lines)
            {
                Bundle bundle = helper.GenerateBundle(line, autoPublish);
                string output = @"C:\Users\Administrator\TDR-2025-CNS6\" + bundle.Uuid + ".tar.gz";
                System.IO.File.WriteAllBytes(output, bundle.TarGz);
                System.Console.WriteLine(bundle.Uuid + ".tar.gz");
                System.Console.WriteLine(System.DateTime.Now);
            }
        }

        static void OneForReal(uint id, bool autoPublish = true)
        {

            DotNetEnv.Env.Load();  // required for bucket name

            Helper helper = new()
            {
                PathToCourtMetadataFile = @"C:\Users\Administrator\TDR-2025-CNS6\court_metadata.csv",
                PathDoDataFolder = @"C:\Users\Administrator\TDR-2025-CNS6\"
            };
            Tracker tracker = new Tracker(@"C:\Users\Administrator\TDR-2025-CNS6\uploaded-production.csv");

            List<Metadata.Line> lines = helper.FindLines(id);

            foreach (var line in lines)
            {
                if (tracker.WasDone(line))
                {
                    System.Console.WriteLine("skipping " + line.id);
                    continue;
                }

                Bundle bundle = helper.GenerateBundle(line, autoPublish);

                string output = @"C:\Users\Administrator\TDR-2025-CNS6\" + bundle.Uuid + ".tar.gz";
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

        }

        static void TestTracker(uint id)
        {
            Tracker tracker = new Tracker(@"C:\Users\Administrator\TDR-2025-CNS6\uploaded-production.csv");
            Helper helper = new()
            {
                PathToCourtMetadataFile = @"C:\Users\Administrator\TDR-2025-CNS6\court_metadata.csv",
                PathDoDataFolder = @"C:\Users\Administrator\TDR-2025-CNS6\"
            };
            List<Metadata.Line> lines = helper.FindLines(id);
            System.Console.WriteLine(lines.Count + " lines");
            foreach (var line in lines)
            {
                System.Console.WriteLine(tracker.WasDone(line));
            }
        }

    }

}
