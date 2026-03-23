# Associated Documents Lambda

## Context

The parser supports EM, IA, and EN documents via internal helper classes. These need to be exposed through a new Lambda handler at `TRE/leg/`, following the same pattern as the existing lawmaker Lambda (`TRE/lawmaker/`).

## Parser API

The helper classes are `internal` — add `[InternalsVisibleTo("TRE")]` to `src/Program.cs` (alongside the existing declarations).

```csharp
using EM = UK.Gov.Legislation.ExplanatoryMemoranda;
using IA = UK.Gov.Legislation.ImpactAssessments;
using EN = UK.Gov.Legislation.ExplanatoryNotes;

IXmlDocument result = documentType switch {
    "em" => EM.Helper.Parse(docxBytes, filename),
    "ia" => IA.Helper.Parse(docxBytes, filename),
    "en" => EN.Helper.Parse(docxBytes, filename),
    _ => throw new Exception($"unsupported document type: {documentType}")
};
```

The `filename` parameter is the original filename (e.g. `uksiem_20132911_en.docx`). It's used for metadata/URI lookup.

The return type `IXmlDocument` (`src/leg/XmlDocument.cs`) gives you the serialized AKN XML via `.Serialize()`, and extracted images via `.Images`. Images are `RenamedImage` instances with a `RelativePath` property that mirrors the S3 folder structure expected by legislation.gov.uk.

## CSV mapping limitation

The embedded CSV files (`em_to_legislation_mapping.csv`, `ia_to_legislation_mapping.csv`, `en_to_legislation_mapping.csv`) only cover historically known filenames. New documents arriving via the publishing workflow won't have entries and the parser will throw a `KeyNotFoundException`. You'll need to implement a MarkLogic database call to resolve legislation metadata on the fly for unknown filenames.

## Reference

| | |
|---|---|
| Lawmaker Lambda pattern | `TRE/lawmaker/Lambda.cs`, `Inputs.cs`, `Outputs.cs` |
| Parser entry points | `src/leg/em/Helper.cs`, `src/leg/ia/Helper.cs`, `src/leg/en/Helper.cs` |
| Return type | `src/leg/XmlDocument.cs` (`IXmlDocument`) |
| Image handling | `src/leg/common/LegImageProcessor.cs` (`RenamedImage`) |
| Lambda entry point | `TRE::UK.Gov.NationalArchives.CaseLaw.TRE.Leg.Lambda::FunctionHandler` |
