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
        var courtsJsonString =
            embeddedResourceHelper.GetEmbeddedResourceAsString("NationalArchives.FindCaseLaw.Utils.courts.json");

        var deserializedCourts = JsonSerializer.Deserialize<TopLevelCourt[]>(courtsJsonString);
        if (deserializedCourts is null || deserializedCourts.Length == 0)
        {
            throw new CourtDeserialisationException();
        }

        // Remove any TNA specific courts because they are purely to support the rest of the system and we don't want to accidentally match their citations
        deserializedCourts = deserializedCourts.Select(topLevelCourt =>
            topLevelCourt with
            {
                Courts = topLevelCourt.Courts
                                      .Where(court =>
                                          !court.Code.StartsWith("TNA-", StringComparison.InvariantCultureIgnoreCase))
                                      .ToArray()
            }
        ).ToArray();

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

    public IEnumerable<Court> Where(Func<Court, bool> predicate)
    {
        return allCourts.Where(predicate);
    }
}
