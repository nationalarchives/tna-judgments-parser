
using System.Collections.Generic;

using  UK.Gov.Legislation.Judgments;

namespace UK.Gov.Legislation.Models {

interface IDocument {

    DocumentMetadata Meta { get; }

    IEnumerable<IBlock> Header { get; }

    IEnumerable<IImage> Images { get; }

    IEnumerable<IAnnex> Annexes { get; }

}

interface IDocument<T> : IDocument {

    IEnumerable<T> Body { get; }

}

interface IDividedDocument : IDocument<IDivision> { }

interface IUndividedDocument : IDocument<IBlock> { }

internal class DividedDocument : IDividedDocument {

    public DocumentMetadata Meta { get; internal init; }

    public IEnumerable<IBlock> Header { get; internal init; }

    public IEnumerable<IDivision> Body { get; internal init; }

    public IEnumerable<IAnnex> Annexes { get; internal init; }

    public IEnumerable<IImage> Images { get; internal set; }

}

internal class UndividedDocument : IUndividedDocument {

    public DocumentMetadata Meta { get; internal init; }

    public IEnumerable<IBlock> Header { get; internal init; }

    public IEnumerable<IBlock> Body { get; internal init; }

    public IEnumerable<IAnnex> Annexes { get; internal init; }

    public IEnumerable<IImage> Images { get; internal set; }

}

internal class DocumentMetadata {

    public string Name { get; init; }

    public string ShortUriComponent { get; init; }

    public string WorkUri { get => "http://www.legislation.gov.uk/id/" + ShortUriComponent; }
    public string WorkDate { get; init; }
    public string WorkDateName { get; init; }
    public string WorkAuthor { get; init; }

    public string ExpressionUri { get => "http://www.legislation.gov.uk/" + ShortUriComponent; }
    public string ExpressionDate { get; init; }
    public string ExpressionDateName { get; init; }
    public string ExpressionAuthor { get; init; }

    /// <summary>
    /// The URI of the legislation this document is associated with (e.g., for Impact Assessments).
    /// </summary>
    public string LegislationUri { get; init; }

    /// <summary>
    /// The identifier used to derive image filenames for this document.
    /// Defaults to <see cref="ShortUriComponent"/>. Override in subtypes where the document
    /// has its own identifier separate from the legislation path
    /// (e.g. IAs use 'ukia/{year}/{number}' rather than 'uksi/{year}/{number}/impacts/{year}/{number}').
    /// </summary>
    public virtual string ImageFileIdentifier => ShortUriComponent;

    public Dictionary<string, Dictionary<string, string>> CSS { get; init; }

    public DocumentStatistics Statistics { get; internal set; }

}

internal class DocumentStatistics {

    public int TotalParagraphs { get; init; }

    public int BodyParagraphs { get; init; }

    public int ScheduleParagraphs { get; init; }

    public int TotalImages { get; init; }

}

internal class AnnexMetadata : DocumentMetadata {

    internal AnnexMetadata(DocumentMetadata main, int n) {
        Name = "Annex";
        ShortUriComponent = main.ShortUriComponent + "/annex/" + n;
        WorkAuthor = main.WorkAuthor;
    }

}

}
