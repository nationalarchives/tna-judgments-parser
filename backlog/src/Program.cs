
using Backlog.Src.Batch.One;

namespace Backlog.Src
{

    class Program
    {

        static int Main(string[] args)
        {
            uint id = 143;
            uint bulkNum = BulkNumbers.Next(id);
            byte[] bundle = Metadata.FindLineAndMakeBundle(id, bulkNum);
            string key = "bulk" + bulkNum + ".tar.gz";
//            Bucket.UploadBundle(key, bundle);
            BulkNumbers.Save(id, bulkNum);
            return 0;
        }

    }

}
