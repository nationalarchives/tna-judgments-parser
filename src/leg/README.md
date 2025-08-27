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

### Directory Structure
```
src/leg/
├── common/              # Shared base classes and configuration
│   ├── BaseHelper.cs           # Base class for document helpers
│   ├── BaseLegislativeDocumentParser.cs  # Base parser with common logic
│   ├── BaseHeaderSplitter.cs   # Base header parsing logic
│   ├── BaseMetadata.cs         # Base metadata extraction
│   ├── LegislativeDocumentConfig.cs  # Document-specific configuration
│   └── RegulationNumber.cs     # URI and number extraction logic
├── models/              # Data models and interfaces
│   ├── Document.cs             # Document interfaces (IDocument, IDividedDocument, etc.)
│   ├── Structure.cs            # Structural models (Section, Subheading, etc.)
│   └── Inline.cs               # Inline content models (DocType, DocNumber, etc.)
├── em/                  # Explanatory Memorandum specific
│   ├── Helper.cs               # Public API for EM parsing
│   └── Parser.cs               # EM-specific parser implementation
├── ia/                  # Impact Assessment specific  
│   ├── Helper.cs               # Public API for IA parsing (includes CSS processing)
│   └── Parser.cs               # IA-specific parser implementation
├── schemas/             # XSD validation schemas
│   ├── em-subschema.xsd        # Schema for Explanatory Memorandums
│   └── ia-subschema.xsd        # Schema for Impact Assessments
├── Builder.cs           # Converts parsed documents to AKN XML format
├── Validator.cs         # Validates output against appropriate XSD schemas
├── akn2html.xsl         # Transforms AKN XML to HTML for display
└── XmlDocument.cs       # Output document interface
```

### How It Works

1. **Configuration-Driven**: Each document type has a `LegislativeDocumentConfig` that specifies:
   - Document titles to recognize
   - Word style names for sections and headings  
   - URI suffix (`/em` or `/ia`)
   - Default document type name

2. **Base Class Architecture**: Common parsing logic is in base classes:
   - `BaseHelper` - Entry point and document processing pipeline
   - `BaseLegislativeDocumentParser` - Core parsing logic for sections, headings, etc.
   - `BaseHeaderSplitter` - Document header analysis and splitting
   - `BaseMetadata` - Metadata extraction and URI generation

3. **Document-Specific Customization**: Each document type can:
   - Override parsing methods for special handling
   - Apply document-specific post-processing (e.g., IA CSS classes)
   - Use different configurations while sharing the same base logic

### Validation
The appropriate XSD schema is automatically selected in `Validator.cs` based on the document's `name` attribute:
- `ImpactAssessment` → `ia-subschema.xsd`
- `ExplanatoryMemorandum` or `PolicyNote` → `em-subschema.xsd`

Adding New Document Types
-------------------------

1. **Create configuration**: Add a factory method to `LegislativeDocumentConfig` with document-specific settings
2. **Create document-specific classes**:
   ```
   src/leg/{type}/
   ├── Helper.cs    # Inherit from BaseHelper, override ParseDocument()
   └── Parser.cs    # Inherit from BaseLegislativeDocumentParser, pass config to base
   ```
3. **Create XSD schema**: Add `schemas/{type}-subschema.xsd`
4. **Update validation**: Modify `Validator.cs` to handle the new document type
5. **Add styling**: Add any required CSS classes to `akn2html.xsl`
6. **Update CLI**: Add new hint option to `Program.cs`

The base classes handle all the complex parsing logic automatically - you only need to provide configuration and any document-specific customisations.
