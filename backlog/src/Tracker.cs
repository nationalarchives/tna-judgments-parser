#nullable enable

using System.IO;
using System.Linq;

using Backlog.Csv;
using Backlog.Options;

using Microsoft.Extensions.Options;

namespace Backlog.Src;

internal class Tracker
{
    private readonly string trackerFilePath;

    public Tracker(IOptions<BacklogParserOptions> backlogParserOptions)
    {
        trackerFilePath = backlogParserOptions.Value.TrackerFilePath;
        if (!File.Exists(trackerFilePath))
        {
            using var stream = File.Create(trackerFilePath);
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
        return File.ReadAllLines(trackerFilePath).Any(entry => entry.Contains(key));
    }

    internal void MarkDone(CsvLine line, string uuid)
    {
        var key = MakeKey(line);
        var timestamp = System.DateTime.Now.ToFileTime();
        var next = key + "," + uuid + "," + timestamp;
        File.AppendAllLines(trackerFilePath, [next]);
    }
}
