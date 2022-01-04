
using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace UK.Gov.Legislation.Judgments.Parse {

class WLine : ILine {

    private readonly MainDocumentPart main;
    private readonly ParagraphProperties properties;
    private IEnumerable<IInline> contents;

    internal bool IsFirstLineOfNumberedParagraph { get; init; }

    public WLine(MainDocumentPart main, ParagraphProperties properties, IEnumerable<IInline> contents) {
        this.main = main;
        this.properties = properties;
        this.contents = contents;
    }
    public WLine(MainDocumentPart main, Paragraph paragraph) {
        this.main = main;
        this.properties = paragraph.ParagraphProperties;
        this.contents = Inline.ParseRuns(main, paragraph.ChildElements);
    }
    public WLine(WLine prototype, IEnumerable<IInline> contents) {
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = contents;
        IsFirstLineOfNumberedParagraph = prototype.IsFirstLineOfNumberedParagraph;
    }
    internal WLine(WLine prototype) {
        this.main = prototype.main;
        this.properties = prototype.properties;
        this.contents = prototype.contents;
        IsFirstLineOfNumberedParagraph = prototype.IsFirstLineOfNumberedParagraph;
    }

    public string Style {
        get => properties?.ParagraphStyleId?.Val;
    }

    public AlignmentValues? Alignment {
        get {
            Justification just = properties?.Justification;
            if (just is null)
                return null;
            if (just.Val.Equals(JustificationValues.Left))
                return AlignmentValues.Left;
            if (just.Val.Equals(JustificationValues.Right))
                return AlignmentValues.Right;
            if (just.Val.Equals(JustificationValues.Center))
                return AlignmentValues.Center;
            if (just.Val.Equals(JustificationValues.Both))
                return AlignmentValues.Justify;
            return null;
        }
    }

    public float? LeftIndentInches {
        get => DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, properties);
    }
    public string LeftIndent {
        get {
            float? inches = DOCX.Paragraphs.GetLeftIndentWithNumberingAndStyleInInches(main, properties);
            if (inches is null)
                return null;
            return inches.Value.ToString("F2") + "in";
        }
    }
    public string RightIndent {
        get {
            if (properties?.Indentation?.Right is null)
                return null;
            float inches = float.Parse(properties.Indentation.Right.Value) / 1440f;
            return inches.ToString("F2") + "in";
        }
    }
    public float? FirstLineIndentInches {
        get {
            float? relative = DOCX.Paragraphs.GetFirstLineIndentWithStyleButNotNumberingInInches(main, properties);
            if (!IsFirstLineOfNumberedParagraph)
                return relative;
            const float min = 0.5f;
            if (!relative.HasValue)
                return null;    // ewhc/fam/2021/3004
            float? firstTab = DOCX.Paragraphs.GetFirstTab(main, properties);
            if (!firstTab.HasValue) {
                if (relative.Value < 0.0f && Math.Abs(relative.Value) < min)
                    return 0.0f;    // ewhc/qb/2021/3437
                    // this is more accurate but might it make large numbers difficult to read?
                return relative.Value + min;
            }
            float leftIndent = this.LeftIndentInches ?? 0f;
            float hardFirst = leftIndent + relative.Value;
            float firstTabRelative = firstTab.Value - hardFirst;
            if (firstTabRelative > min)
                return relative.Value + min;
            // if (firstTabRelative.Value > Math.Abs(relative.Value))
            //     return relative.Value + min;
            return relative.Value + firstTabRelative;
        }
    }
    // public float? FirstLineIndentInches {
    //     get {
    //         float? relative = DOCX.Paragraphs.GetFirstLineIndentWithStyleButNotNumberingInInches(main, properties);
    //         if (!relative.HasValue)
    //             return null;
    //         // if (IsFirstLineOfNumberedParagraph && relativeFirst.HasValue)
    //         //     System.Console.WriteLine("first line indent is " + x);
    //         // if (IsFirstLineOfNumberedParagraph && x.HasValue && x.Value == 0f)
    //         //     System.Console.WriteLine("first line indent is 0");
    //         // if (IsFirstLineOfNumberedParagraph && x.HasValue && x.Value < 0f)
    //         //     System.Console.WriteLine("first line indent is negative");
    //         if (IsFirstLineOfNumberedParagraph && relative.Value > 0f && relative.Value < 0.5f) {
    //             // *** the reason to add a value when there is none can be seen in EWHC/Admin/2020/602.pdf
    //             System.Console.WriteLine("first line indent is small");
    //             return 0.5f;
    //         }
    //         if (IsFirstLineOfNumberedParagraph && relative.Value < 0f && relative.Value > -0.1f) {
    //             // *** the reason to add a value when there is none can be seen in EWHC/Admin/2020/602.pdf
    //             System.Console.WriteLine("first line indent is small");
    //             return 0.5f;
    //         }
    //         if (IsFirstLineOfNumberedParagraph && relative.HasValue && relative.Value > 0.5f) {
    //             System.Console.WriteLine("first line indent is big");
    //             return relative.Value - 0.5f;
    //         }
    //         if (IsFirstLineOfNumberedParagraph && relative.HasValue && relative.Value < -0.5f) {
    //             System.Console.WriteLine("first line indent is big and negative");
    //             // EWHC/Admin/2013/3527.rtf
    //             float leftIndent = this.LeftIndentInches ?? 0f;
    //             float hardFirst = leftIndent + relative.Value;
    //             float? firstTab = DOCX.Paragraphs.GetFirstTab(main, properties);
    //             float? firstTabRelative = firstTab is null ? null : firstTab - hardFirst;
    //             float y;
    //             if (!firstTabRelative.HasValue)
    //                 y = relative.Value + 0.5f;
    //             else if (firstTabRelative.Value > Math.Abs(relative.Value))
    //                 y = relative.Value + 0.5f;
    //             else
    //                 y = relative.Value + firstTabRelative.Value;
    //             return y;
    //         }
    //         if (IsFirstLineOfNumberedParagraph)
    //             return null;
    //         return relative;
    //     }
    // }
    public string FirstLineIndent {
        get {
            float? inches = this.FirstLineIndentInches;
            if (!inches.HasValue)
                return null;
            return inches.Value.ToString("F2") + "in";

            // !!! maybe the key is that Old numbered paragraphs are different

            // if negative and small, all is ok EWCA/Crim/2007/1005

            // *** the reason to add a value when there is none can be seen in EWHC/Admin/2020/602.pdf

            // float? inches = DOCX.Paragraphs.GetFirstLineIndentWithStyleButNotNumberingInInches(main, properties);
            // if (!inches.HasValue)
            //     return IsFirstLineOfNumberedParagraph ? "0.5in" : null;
            // if (IsFirstLineOfNumberedParagraph && inches < 0.0f)
            //     return null;
            // // when number and first line indent is 0, default indent is a tab
            // if (IsFirstLineOfNumberedParagraph)
            //     inches = inches.Value + 0.5f;
            // // if (IsFirstLineOfNumberedParagraph && inches < 0.5f)
            // //     return "0.5in";
            // return inches.Value.ToString("F2") + "in";
        }
    }

    public IEnumerable<IInline> Contents {
        get => contents;
        set { contents = value; }
    }

}

class WRestriction : WLine, IRestriction {

    internal WRestriction(WLine line) : base(line) { }

}

}
