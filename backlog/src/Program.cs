
using Backlog.Src.Batch.One;

namespace Backlog.Src
{

    class Program
    {

        static int Main(string[] args)
        {
            Batch.One.Helper helper = new() {
                PathToCourtMetadataFile = @"C:\Users\Administrator\TDR-2025-CNS6\court_metadata.csv",
                PathDoDataFolder = @"C:\Users\Administrator\TDR-2025-CNS6\"
            };
            // Files.CopyAllFilesWithExtension(helper.PathDoDataFolder, Metadata.Read(helper.PathToCourtMetadataFile));

            // Bundle bundle = helper.GenerateBundle(65);

            // string output = @"C:\Users\Administrator\TDR-2025-CNS6\test.tar.gz";
            // System.IO.File.WriteAllBytes(output, bundle.TarGz);

            return 0;
        }

    }

}
