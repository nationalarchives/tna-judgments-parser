#nullable enable

using System;

namespace UK.Gov.Legislation.Lawmaker;

/// <summary>
/// Thrown when a stage of the Lawmaker parse fails outside block parsing, so the error
/// returned to Lawmaker can say which stage failed and, where known, which block caused it.
/// </summary>
public class ParseStageException : Exception
{

    public const string ReadingDocument = "reading the document";
    public const string ParsingStructure = "parsing the document structure";
    public const string ExtractingQuotes = "extracting quotes";
    public const string EnrichingQuotations = "enriching quotations";
    public const string EnrichingFootnotes = "enriching footnotes";
    public const string BuildingTable = "building a table";
    public const string BuildingXml = "building the XML";
    public const string SimplifyingXml = "simplifying the XML";
    public const string SerializingXml = "serializing the XML";
    public const string ExtractingImages = "extracting images";

    public string Stage { get; }

    public int? BlockNumber { get; }

    public string? BlockText { get; }

    public ParseStageException(string stage, int? blockNumber, string? blockText, Exception inner)
        : base($"error {stage}", inner)
    {
        Stage = stage;
        BlockNumber = blockNumber;
        BlockText = blockText;
    }

    internal static void Wrap(string stage, Action action) => Wrap(stage, () =>
    {
        action();
        return 0;
    });

    // A failure that has already been attributed to a stage is passed on unchanged,
    // so the innermost and most specific stage is the one reported.
    internal static T Wrap<T>(string stage, Func<T> func)
    {
        try
        {
            return func();
        }
        catch (Exception ex) when (ex is not ParseStageException and not BlockParsingException)
        {
            throw new ParseStageException(stage, null, null, ex);
        }
    }

}
