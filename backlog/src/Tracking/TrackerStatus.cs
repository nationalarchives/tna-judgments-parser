#nullable enable
namespace Backlog.Tracking;

public enum TrackerStatus
{
    Started,
    Parsed,
    ParserFailed,
    SentToIngester,
    Ingested,
    IngesterFailed,
    Published,
    PublicationFailed
}
