
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
            NextForReal();
            // OneLocal(113);
            return 0;
        }

        static Helper helper = new()
        {
            PathToCourtMetadataFile = @"C:\Users\Administrator\TDR-2025-CNS6\metadata_v4.csv",
            PathToDataFolder = @"C:\Users\Administrator\TDR-2025-CNS6\"
        };

        static Tracker tracker = new Tracker(@"C:\Users\Administrator\TDR-2025-CNS6\uploaded-production.csv");

        static void NextLocal(int n = 1)
        {
            int done = 0;
            List<Metadata.Line> lines = Metadata.Read(helper.PathToCourtMetadataFile);
            foreach (var line in lines)
            {
                if (done >= n)
                    break;
                if (tracker.WasDone(line))
                {
                    System.Console.WriteLine("skipping " + line.id);
                    continue;
                }
                if (line.ShouldSkip())
                {
                    System.Console.WriteLine("skipping " + line.id);
                    continue;
                }
                Bundle bundle = helper.GenerateBundle(line, true);
                string output = @"C:\Users\Administrator\TDR-2025-CNS6\250715_test\" + bundle.Uuid + ".tar.gz";
                System.IO.File.WriteAllBytes(output, bundle.TarGz);
                System.Console.WriteLine("done " + line.id + " --> " + bundle.Uuid + ".tar.gz");
                done += 1;
            }
        }

        static void OneLocal(uint id, bool autoPublish = false)
        {
            System.Console.WriteLine("OneLocal " + id);
            List<Metadata.Line> lines = helper.FindLines(id);
            foreach (var line in lines)
            {
                Bundle bundle = helper.GenerateBundle(line, autoPublish);
                string output = @"C:\Users\Administrator\TDR-2025-CNS6\250715_test\" + bundle.Uuid + ".tar.gz";
                System.IO.File.WriteAllBytes(output, bundle.TarGz);
                System.Console.WriteLine("done " + line.id + " --> " + bundle.Uuid + ".tar.gz");
            }
        }

        static void NextForReal(int n = 1, bool autoPublish = true)
        {
            DotNetEnv.Env.Load();

            int done = 0;
            List<Metadata.Line> lines = Metadata.Read(helper.PathToCourtMetadataFile);
            foreach (var line in lines)
            {
                if (done >= n)
                    break;
                if (tracker.WasDone(line))
                {
                    System.Console.WriteLine("skipping " + line.id + " because it's already been done");
                    continue;
                }
                if (line.ShouldSkip())
                {
                    System.Console.WriteLine("skipping " + line.id + " because it's marked as 'skip'");
                    continue;
                }
                System.Console.WriteLine("bundling " + line.id);
                Bundle bundle = helper.GenerateBundle(line, autoPublish);

                System.Console.WriteLine("saving " + line.id + " --> " + bundle.Uuid + ".tar.gz");
                string output = @"C:\Users\Administrator\TDR-2025-CNS6\uploaded\" + bundle.Uuid + ".tar.gz";
                System.IO.File.WriteAllBytes(output, bundle.TarGz);

                System.Console.WriteLine("uploading " + line.id + " --> " + bundle.Uuid + ".tar.gz");
                Task<PutObjectResponse> task = Bucket.UploadBundle(bundle.Uuid + ".tar.gz", bundle.TarGz);
                var x = task.Result;
                tracker.MarkDone(line, bundle.Uuid);

                System.Console.WriteLine("success");
                System.Console.WriteLine(bundle.Uuid + ".tar.gz");
                System.Console.WriteLine(System.DateTime.Now);

                done += 1;
            }
        }

        static void OneForReal(uint id, bool autoPublish = true)
        {

            DotNetEnv.Env.Load();  // required for bucket name

            Helper helper = new()
            {
                PathToCourtMetadataFile = @"C:\Users\Administrator\TDR-2025-CNS6\court_metadata.csv",
                PathToDataFolder = @"C:\Users\Administrator\TDR-2025-CNS6\"
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
                PathToDataFolder = @"C:\Users\Administrator\TDR-2025-CNS6\"
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
