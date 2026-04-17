#nullable enable

using System.IO;
using System.Linq;

using Backlog.Csv;

namespace Backlog.Src;

internal class Tracker
{
    private readonly string file;

    internal Tracker(string file)
    {
        this.file = file;
        if (!File.Exists(file))
        {
            using var stream = File.Create(file);
            stream.Close();
        }
    }

    private string MakeKey(CsvLine line)
    {
        return line.id + "/" + line.FilePath;
    }

    internal bool WasDone(CsvLine line)
    {
        var key = MakeKey(line);
        return File.ReadAllLines(file).Any(entry => entry.Contains(key));
    }

    internal void MarkDone(CsvLine line, string uuid)
    {
        var key = MakeKey(line);
        var timestamp = System.DateTime.Now.ToFileTime();
        var next = key + "," + uuid + "," + timestamp;
        File.AppendAllLines(file, [next]);
    }
}
