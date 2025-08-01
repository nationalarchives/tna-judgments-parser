
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

using Amazon.S3.Model;

using Backlog.Src.Batch.One;

namespace Backlog.Src
{

    public class Program
    {

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length < 2 || args[0] != "--id" || !uint.TryParse(args[1], out uint id))
                {
                    System.Console.WriteLine("Usage: backlog --id <id>");
                    return 1;
                }

                bool autoPublish = true;

                DotNetEnv.Env.Load();  // required for bucket name

                Helper helper = new()
                {
                    PathToCourtMetadataFile = Environment.GetEnvironmentVariable("COURT_METADATA_PATH") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "court_metadata.csv"),
                    PathToDataFolder = Environment.GetEnvironmentVariable("DATA_FOLDER_PATH") ?? AppDomain.CurrentDomain.BaseDirectory
                };
                string trackerPath = Environment.GetEnvironmentVariable("TRACKER_PATH") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploaded-production.csv");
                Tracker tracker = new Tracker(trackerPath);

                List<Metadata.Line> lines = helper.FindLines(id);
                if (!lines.Any())
                {
                    System.Console.WriteLine($"No records found for id {id}");
                    return 1;
                }

                foreach (var line in lines)
                {
                    if (tracker.WasDone(line)) {
                        System.Console.WriteLine("skipping " + line.id);
                        continue;
                    }

                    try
                    {
                        System.Console.WriteLine($"Processing file: {line.FilePath}");
                        System.Console.WriteLine($"Using court metadata from: {helper.PathToCourtMetadataFile}");
                        System.Console.WriteLine($"Using data folder: {helper.PathToDataFolder}");
                        
                        Bundle bundle = helper.GenerateBundle(line, autoPublish);

                        string outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? AppDomain.CurrentDomain.BaseDirectory;
                        string output = Path.Combine(outputPath, bundle.Uuid + ".tar.gz");
                        System.IO.File.WriteAllBytes(output, bundle.TarGz);

                        System.Console.WriteLine(bundle.Uuid + ".tar.gz");
                        System.Console.WriteLine(System.DateTime.Now);

                        Task<PutObjectResponse> task = Bucket.UploadBundle(bundle.Uuid + ".tar.gz", bundle.TarGz);
                        var response = task.Result;

                        tracker.MarkDone(line, bundle.Uuid);

                        System.Console.WriteLine("success");
                        System.Console.WriteLine(bundle.Uuid + ".tar.gz");
                        System.Console.WriteLine(System.DateTime.Now);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error processing line {line.id}: {ex.Message}");
                        return 1;
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Fatal error: {ex.Message}");
                return 1;
            }
        }

    }

}
