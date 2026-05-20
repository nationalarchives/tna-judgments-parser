using UK.Gov.NationalArchives.CaseLaw.Parse;

namespace UK.Gov.Legislation.Common {

/// <summary>
/// PreParser configured for legislation .docx files: TOC closers
/// commonly share a paragraph with the next heading.
/// </summary>
class LegPreParser : PreParser {

    protected override bool ReprocessFieldEndWithContent => true;

}

}
