Legislation parser
==================

This parser converts UK legislation documents from .docx format to XML. It handles Explanatory Memorandums (EMs) and Impact Assessments (IAs), with support for additional document types.

Document Types
--------------

### Explanatory Memorandums (EMs)
Located in [em/](./em/), these documents explain the purpose and effect of statutory instruments.

### Impact Assessments (IAs) 
Located in [ia/](./ia/), these documents assess the impact of proposed government policies and regulations.

C# API
------

To parse legislation documents programmatically, use the appropriate Helper class:

**Explanatory Memorandums:**
```csharp
using UK.Gov.Legislation.ExplanatoryMemoranda;
var result = Helper.Parse(docxStream);
```

**Impact Assessments:**
```csharp
using UK.Gov.Legislation.ImpactAssessments;
var result = Helper.Parse(docxStream);
```

Both return an `IXmlDocument` object containing the parsed AKN XML.

CLI
---

Use the `--hint` parameter to specify the document type:

    dotnet run --input path/to/em.docx --hint em
    dotnet run --input path/to/ia.docx --hint ia

Architecture
------------

### Shared Components
- `Builder.cs` - Converts parsed documents to AKN XML format
- `Validator.cs` - Validates output against appropriate XSD schemas
- `akn2html.xsl` - Transforms AKN XML to HTML for display

### Document-Specific Structure
Each document type follows this pattern:
```
{type}/
├── Helper.cs      # Public API for parsing
├── Parser.cs      # Document-specific parsing logic  
├── Metadata.cs    # Metadata extraction and URI generation
└── Header.cs      # Header parsing and document identification
```

### Schemas
- `subschema.xsd` - XSD schema for Explanatory Memorandums
- `ia-subschema.xsd` - XSD schema for Impact Assessments

The appropriate schema is automatically selected based on the document type during validation.

Adding New Document Types
-------------------------

1. Create a new folder under `src/leg/{type}/`
2. Implement the required classes: `Helper.cs`, `Parser.cs`, `Metadata.cs`, `Header.cs`
3. Create an XSD schema: `{type}-subschema.xsd`
4. Add the schema as an embedded resource in `judgments.csproj`
5. Update `Validator.cs` to handle the new document type
6. Add any required CSS classes to `akn2html.xsl`
7. Update this README with the new document type
