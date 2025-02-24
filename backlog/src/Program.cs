
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
            Bundle bundle = helper.GenerateBundle(87);
            System.Console.WriteLine(bundle.Data.Parameters.TRE.Payload.Filename);
            return 0;
        }

    }

}
