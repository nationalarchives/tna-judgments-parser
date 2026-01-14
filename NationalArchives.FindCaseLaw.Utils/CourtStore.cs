using System.Collections.Immutable;
using System.Text.Json;

namespace NationalArchives.FindCaseLaw.Utils;

public class CourtStore
{
    private readonly ImmutableArray<Court> allCourts;

    /// <summary>
    ///     Use this when dependency injection is not available
    /// </summary>
    public CourtStore() : this(new EmbeddedResourceHelper()) { }

    /// <summary>
    ///     Prefer this method of instantiation
    /// </summary>
    public CourtStore(IEmbeddedResourceHelper embeddedResourceHelper)
    {
        var courtsJsonString = embeddedResourceHelper.GetEmbeddedResourceAsString("NationalArchives.FindCaseLaw.Utils.courts.json");

        var deserializedCourts = JsonSerializer.Deserialize<TopLevelCourt[]>(courtsJsonString);
        if (deserializedCourts is null || deserializedCourts.Length == 0)
        {
            throw new CourtDeserialisationException();
        }

        var topLevelCourts = deserializedCourts.ToImmutableArray();
        allCourts = topLevelCourts.SelectMany(t => t.Courts).ToImmutableArray();
    }

    public bool Exists(string courtCode)
    {
        return allCourts.Any(c => string.Equals(c.Code, courtCode, StringComparison.InvariantCultureIgnoreCase));
    }

    public Court Get(string courtCode)
    {
        if (Exists(courtCode))
        {
            return allCourts.Single(c =>
                string.Equals(c.Code, courtCode, StringComparison.InvariantCultureIgnoreCase));
        }

        throw new CourtNotFoundException($"Couldn't find court with code {courtCode}");
    }
}
