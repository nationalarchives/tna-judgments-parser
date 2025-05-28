
using System.IO;
using System.Linq;

namespace Backlog.Src.Batch.One
{

    class Tracker
    {
        private readonly string file;
        internal Tracker(string file) {
            this.file = file;
            if (!File.Exists(file)) {
                using var stream = File.Create(file);
                stream.Close();
            }
        }

        private string MakeKey(Metadata.Line line) {
            return line.id + "/" + line.FilePath;
        }

        internal bool WasDone(Metadata.Line line) {
            var key = MakeKey(line);
            return File.ReadAllLines(file).Where(line => line.Contains(key)).Any();
        }

        internal void MarkDone(Metadata.Line line, string uuid) {
            var key = MakeKey(line);
            var timestamp = System.DateTime.Now.ToFileTime();
            string next = key + "," + uuid + "," + timestamp;
            File.AppendAllLines(file, [next]);
        }

    }

}
