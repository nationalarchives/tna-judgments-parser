#nullable enable

using System.Text;

using UK.Gov.Legislation.Judgments;
using UK.Gov.Legislation.Judgments.Parse;

namespace UK.Gov.NationalArchives.CaseLaw.Parsers.UKUT;

/// <summary>
/// Follows the inlines of a run of lines to decide which type each proceedings identifier has: the label directly
/// before it if there is one, otherwise the most recent label seen.
/// </summary>
internal interface IProceedingsIdentifierTypeTracker
{
    void Reset();

    void StartLine();

    void Record(IInline inline);

    string? GetCurrentType();
}

internal class ProceedingsIdentifierTypeTracker : IProceedingsIdentifierTypeTracker
{
    private readonly IProceedingsIdentifierTypeLabelParser labelParser;
    private readonly StringBuilder textSinceLastIdentifier = new();
    private string? currentProceedingsIdentifierType;

    public ProceedingsIdentifierTypeTracker() : this(new ProceedingsIdentifierTypeLabelParser())
    {
    }

    internal ProceedingsIdentifierTypeTracker(IProceedingsIdentifierTypeLabelParser labelParser)
    {
        this.labelParser = labelParser;
    }

    public void Reset()
    {
        textSinceLastIdentifier.Clear();
        currentProceedingsIdentifierType = null;
    }

    public void StartLine() => textSinceLastIdentifier.Clear();

    public void Record(IInline inline)
    {
        switch (inline)
        {
            case WText text:
                textSinceLastIdentifier.Append(text.Text);
                break;
            case WTab:
                textSinceLastIdentifier.Append('\t');
                break;
        }
    }

    public string? GetCurrentType()
    {
        currentProceedingsIdentifierType = labelParser.ParseFromPrecedingText(textSinceLastIdentifier.ToString())
                                           ?? currentProceedingsIdentifierType;
        textSinceLastIdentifier.Clear();

        return currentProceedingsIdentifierType;
    }
}
