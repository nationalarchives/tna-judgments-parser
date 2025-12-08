
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using UK.Gov.Legislation.Judgments;
using UK.Gov.NationalArchives.Judgments.Api;

namespace Backlog.Src
{

    public class Program
    {

        public static int Main(string[] args)
        {
            try
            {
               uint? id = null;
                
                // Parse arguments - --id is optional
                if (args.Length == 0)
                {
                    // No arguments - process all records
                    id = null;
                }
                else if (args.Length == 2 && args[0] == "--id")
                {
                    if (!uint.TryParse(args[1], out uint parsedId))
                    {
                        System.Console.WriteLine("Usage: dotnet run [--id <id>]");
                        System.Console.WriteLine("Error: Invalid ID format");
                        return 1;
                    }
                    id = parsedId;
                }
                else
                {
                    System.Console.WriteLine("Usage: dotnet run [--id <id>]");
                    System.Console.WriteLine("Examples:");
                    System.Console.WriteLine("  dotnet run       - Process all records");
                    System.Console.WriteLine("  dotnet run --id 4 - Process only record with ID 4");
                    return 1;
                }

                bool autoPublish = true;

                DotNetEnv.Env.Load();  // required for bucket name

                string judgmentsFilePath = Environment.GetEnvironmentVariable("JUDGMENTS_FILE_PATH") ?? "";
                string hmctsFilePath = Environment.GetEnvironmentVariable("HMCTS_FILES_PATH") ?? "";

                Helper helper = new(new Parser(Logging.Factory.CreateLogger<Parser>(), new UK.Gov.Legislation.Judgments.AkomaNtoso.Validator()))
                {
                    PathToCourtMetadataFile = Environment.GetEnvironmentVariable("COURT_METADATA_PATH") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "court_metadata.csv"),
                    PathToDataFolder = Environment.GetEnvironmentVariable("DATA_FOLDER_PATH") ?? AppDomain.CurrentDomain.BaseDirectory
                };
                string trackerPath = Environment.GetEnvironmentVariable("TRACKER_PATH") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uploaded-production.csv");
                Tracker tracker = new Tracker(trackerPath);

                List<Metadata.Line> lines;
                if (id.HasValue)
                {
                    // Process only the specific ID
                    lines = helper.FindLines(id.Value);
                    if (!lines.Any())
                    {
                        System.Console.WriteLine($"No records found for id {id.Value}");
                        return 1;
                    }
                }
                else
                {
                    // Process all lines from the document
                    lines = Metadata.Read(helper.PathToCourtMetadataFile);
                    if (!lines.Any())
                    {
                        System.Console.WriteLine("No records found in the metadata file");
                        return 1;
                    }
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
                        
                        Bundle bundle = helper.GenerateBundle(line, judgmentsFilePath, hmctsFilePath, autoPublish);

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
                        System.Console.WriteLine($"Error processing line {line.id}:");
                        System.Console.WriteLine(ex.ToString());
                        return 1;
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Fatal error:");
                System.Console.WriteLine(ex.ToString());
                return 1;
            }
        }

    }

}
