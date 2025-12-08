# TNA Judgments Parser - Backlog Module

This module provides a specialized entry point to the parser specifically designed for processing historic tribunal judgments. It handles the unique challenges of importing judgments from legacy tribunal systems, where the source materials and metadata come in various formats.

## Overview

### Purpose

This module processes historic tribunal judgments that are being migrated from various legacy systems. These judgments come with their own unique characteristics:

- **Mixed Source Formats**:
  - PDF-only documents (where XML conversion isn't attempted due to complexity)
  - DOC files (requiring preprocessing to convert to DOCX)
  - DOCX files (ready for direct processing)

### Metadata Handling

- Judgment metadata comes from accompanying spreadsheets specific to each tribunal
- Current implementation focuses on processing one specific tribunal batch
- The module will be expanded to handle other tribunal batches, each with potentially different metadata structures
- Metadata mapping is handled through structured CSV files that maintain consistency in the import process

### Future Development

While the current implementation is focused on a single tribunal's batch processing requirements, the module is being designed to be extensible for:

- Different tribunal-specific metadata formats
- Various source document types
- Custom preprocessing requirements per tribunal
- Batch-specific validation rules

## Configuration

The module uses environment variables for configuration. All paths default to the application's base directory if not specified.

### AWS Configuration

The module requires access to an S3 bucket for storing processed judgments. The AWS SDK for .NET will automatically use:

1. The region from the `AWS_REGION` environment variable if set
2. The region configured in the AWS deployment environment (EC2, Lambda, ECS, etc.)
3. The region configured in your local AWS CLI configuration

For local development or CI environments where AWS configuration isn't automatically available, you'll need to set `AWS_DEFAULT_REGION` (e.g., 'eu-west-2' for London) to match your S3 bucket's region.

### Environment Variables

| Variable              | Description                                                                                            | Default                                                         |
|-----------------------|--------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------|
| `COURT_METADATA_PATH` | Path to the CSV file containing court metadata                                                         | `{BaseDir}/court_metadata.csv`                                  |
| `DATA_FOLDER_PATH`    | Path to the folder containing judgment data files                                                      | `{BaseDir}`                                                     |
| `TRACKER_PATH`        | Path to the CSV file tracking uploaded judgments                                                       | `{BaseDir}/uploaded-production.csv`                             |
| `OUTPUT_PATH`         | Path where generated bundle files will be saved                                                        | `{BaseDir}`                                                     |
| `AWS_REGION`          | AWS region for S3 bucket operations                                                                    | Defaults to the region configured in AWS deployment environment |
| `JUDGMENTS_FILE_PATH` | The filepath prefix used in the court metadata csv (used to crossference with file-metadata.csv paths) | `""`                                                            |
| `HMCTS_FILES_PATH`    | The filepath prefix used in the file-metadata csv (used to crossference with court metadata csv paths) | `""`                                                            |

Note: The `AWS_REGION` variable (e.g., 'eu-west-2' for London) is typically automatically set in AWS deployment environments (EC2, Lambda, ECS, etc.). You only need to set it manually when running locally or in environments without AWS configuration. In CI/CD pipelines, ensure this is set to match your S3 bucket's region.

where `{BaseDir}` is the application's base directory (`AppDomain.CurrentDomain.BaseDirectory`).

## File Formats

### Court Metadata CSV

Contains metadata extracted from tribunal-specific spreadsheets about courts and their judgments. This data includes:

- Court identifiers and names
- Judgment dates
- Case numbers
- Document metadata from the source tribunal system
- Any tribunal-specific metadata fields

The current implementation is tailored to one specific tribunal's metadata format, but the structure allows for expansion to handle different metadata schemas from other tribunals.

#### Required Columns

The CSV file must contain the following columns (case-sensitive) for each judgment:

- `id` - Unique identifier for each judgment record
- `court` - Court code that maps to a Court object (e.g., "EWHC-QBD-Admin", "UKFTT-GRC"). Must match a valid court code defined in the Courts.ByCode dictionary
- `FilePath` - Path to the judgment file relative to the base directory (UUID without extension)
- `Extension` - File extension indicating the original file type (.pdf, .docx, .doc)
- `decision_datetime` - Date and time when the decision was made (format: "yyyy-MM-dd HH:mm:ss")
- `CaseNo` - Case number(s) (with space inbetween if multiple)
- `claimants` - Name(s) of the claimant(s)
- `respondent` - Name(s) of the respondent(s)

#### Optional Columns

The following columns are optional:

- `main_category` - Primary category name
- `main_subcategory` - Primary subcategory name (child of main_category)
- `sec_category` - Secondary category name (optional)
- `sec_subcategory` - Secondary subcategory name (child of sec_category, only used if sec_category is provided)
- `ncn` - Neutral Citation Number (NCN) for the judgment, when available. If provided, this appears as `uk:cite` in the generated AkomaNtoso XML
- `headnote_summary` - Summary of the judgment (included in metadata JSON but not in XML output)

**Note**: Column names are case-sensitive. If required columns are missing, the system will throw a validation error listing the missing columns.

### Tracker CSV

Tracks which judgments have been uploaded to production, preventing duplicate processing. This is particularly important for batch processing where:

- Multiple runs might be needed to process all files
- Some files might fail and need reprocessing
- Source files might be updated and need reprocessing

## Court Codes

The `court` field in the CSV must contain a valid court code that corresponds to a Court object defined in the system. Court codes are used to identify specific courts and tribunals.

### Court Code Validation

- All available court codes are defined in `src/model/Courts.cs`
- Court codes are case-sensitive and must exactly match those **defined in** `Courts.ByCode`
- Invalid court codes will cause processing to fail with a `KeyNotFoundException`

### Common Court Codes

Examples of valid court codes include:

- `EWHC-QBD-Admin` - Administrative Court (Queen's Bench Division)
- `EWHC-Chancery` - Chancery Division  
- `EWHC-Family` - Family Division
- `UKFTT-GRC` - First-tier Tribunal (General Regulatory Chamber)
- `UKIST` - Immigration Services Tribunal (pre-2010)

## Implementation Details

### Directory Structure

The expected directory structure for data processing is:

```plaintext
data/
├── tdr_metadata/
│   └── file-metadata.csv              # Maps original judgment filenames to UUIDs
└── court_documents/                   # Contains the actual judgment documents
    ├── {uuid}                        # UUID-named files without extensions
    ├── {uuid}                        # (e.g., a1b2c3d4-e5f6-7890-abcd-ef1234567890)
    └── {uuid}                        # All files stored using UUID as filename
```

**Important Notes on File Naming:**

- All files in `court_documents/` are named using only their UUID (no file extension)
- The file extension information is stored in the court metadata CSV for reference
- The parser determines file type processing based on the `Extension` field in the metadata, not from the actual filename

### File Processing

The module implements robust file handling with several key features:

1. **UUID-based File Management**:
   - Files are stored internally using UUIDs without file extensions
   - Original filenames are mapped to UUIDs in `file-metadata.csv`
   - The actual files in the `court_documents/` directory are named using just the UUID (e.g., `a1b2c3d4-e5f6-7890-abcd-ef1234567890`)
   - File extensions are tracked separately in the court metadata CSV

2. **Extension Handling and File Type Processing**:
   - **PDF files** (`.pdf`): Processed as-is, wrapped in a simple XML structure for output
   - **DOCX files** (`.docx`): Processed through the full parser to generate rich XML output  
   - **DOC files** (`.doc`): **Must be manually pre-converted to DOCX format before processing**

3. **Pre-conversion Requirements for DOC Files**:
   - The backlog parser **does not** perform DOC to DOCX conversion
   - If a record in the metadata CSV has `Extension` set to `.doc`, the corresponding file in the `court_documents/` directory must already be in DOCX format
   - The original file type (`.doc`) is preserved in the metadata for tracking purposes, but the actual file content must be in DOCX format
   - This pre-conversion should be done using external tools (e.g., LibreOffice, Microsoft Word automation) before running the backlog parser

   **Example for DOC files**:

   ```plaintext
   Court Metadata CSV entry:
   FilePath: a1b2c3d4-e5f6-7890-abcd-ef1234567890
   Extension: .doc
   
   Expected file in court_documents/:
   Filename: a1b2c3d4-e5f6-7890-abcd-ef1234567890 (no extension)
   Content: Must be in DOCX format (pre-converted from original DOC)
   ```

4. **File Location and Naming**:
   - Files are located using the UUID from the `FilePath` field in the court metadata
   - The UUID is used directly as the filename (without any extension) in the `court_documents/` directory
   - Example: If `FilePath` contains `a1b2c3d4-e5f6-7890-abcd-ef1234567890`, the parser looks for a file named exactly `a1b2c3d4-e5f6-7890-abcd-ef1234567890` in the `court_documents/` directory

5. **Error Handling**:
   - Continues processing on individual file errors
   - Logs issues for manual review
   - Maintains processing state for partial batch completion

### Batch Processing

The module processes files in batches, supporting both individual record processing and bulk operations. The process flow is:

1. **Metadata Lookup**: Records are found by ID in the court metadata CSV
2. **UUID Resolution**: File UUIDs are looked up in the TDR metadata
3. **File Location**: Files are located in the court_documents directory using UUIDs
4. **Content Processing**: Files are processed according to type (PDF wrapped in XML, DOCX through full parser)

## Usage

The backlog processor supports three modes of operation:

### Processing Modes

1. **Process the Entire CSV** (default):

   ```bash
   dotnet run
   ```

   Processes every record in the court metadata CSV sequentially, skipping already processed records.

2. **Process Specific Records by ID**:

   ```bash
   dotnet run --id <id>
   ```

   Processes a specific judgment record by its ID from the court metadata CSV.

### CSV Line Example

```csv
id,court,FilePath,Extension,decision_datetime,CaseNo,claimants,respondent,main_category,main_subcategory
123,UKFTT-GRC,a1b2c3d4-e5f6-7890-abcd-ef1234567890,.pdf,2025-01-15 09:00:00,GRC/2025/001,Smith,Secretary of State,Immigration,Appeal Rights
124,EWHC-QBD-Admin,b2c3d4e5-f6g7-8901-bcde-f23456789012,.docx,2025-01-16 10:00:00,IA/2025/002,Jones,HMRC,Tax,VAT Appeals
125,UKUT-IAC,c3d4e5f6-g7h8-9012-cdef-34567890123a,.doc,2025-01-17 11:00:00,UKUT/2025/003,Williams,Home Office,Immigration,Entry Clearance
```

- Line 1 = ID 123 (Smith vs Secretary of State)
- Line 2 = ID 124 (Jones vs HMRC)  
- Line 3 = ID 125 (Williams vs Home Office)

### Setup and Execution

1. **Set environment variables**:

   ```bash
   export COURT_METADATA_PATH=/path/to/metadata.csv
   export DATA_FOLDER_PATH=/path/to/data
   export TRACKER_PATH=/path/to/tracker.csv
   export OUTPUT_PATH=/path/to/output
   ```

2. **Prepare data directory structure** as shown in the Implementation Details section.

3. **Run the processor** using one of the modes above.

### Processing Workflow

All modes follow the same workflow:

- Look up records in the court metadata CSV
- Resolve UUIDs and locate files in the court_documents directory
- Process files according to their type (PDF, DOCX, DOC)
- Generate bundles and upload to S3
- Track processed files to prevent duplicates

## Development

### Components

The module consists of several components:

- `Helper.cs`: Main processing logic
- `BulkNumbers.cs`: Handles bulk number assignment and tracking
- `Tracker.cs`: Tracks processed judgments

### Relationship with Main Parser

This module exists as a separate entry point from the main parser ([see main README](../README.md)) for several reasons:

1. **Different Input Processing**:
   - Main parser: Expects single DOCX files with optional attachments
   - Backlog module: Handles batches with mixed formats (PDF, DOC, DOCX) and external metadata

2. **Metadata Handling**:
   - Main parser: Uses inline document metadata or simple key-value pairs
   - Backlog module: Processes complex tribunal-specific metadata from external spreadsheets

3. **Batch Processing**:
   - Main parser: Focuses on single document transformation
   - Backlog module: Manages state across multiple documents, tracks progress, and handles bulk numbering

### Potential for Code Sharing

While keeping separate entry points makes sense, some functionality could be abstracted into the main parser in future PRs:

1. **Document Preprocessing**:
   - DOC to DOCX conversion logic could be useful for the main parser
   - Could be moved to a shared utility class

2. **Extended Metadata Handling**:
   - The metadata mapping system could be generalized
   - Could create an extensible metadata provider interface

3. **Progress Tracking**:
   - The tracking system could be useful for other batch operations
   - Could be abstracted into a reusable component

### Development Guidelines

When adding new features or modifying paths, ensure to:

1. Use environment variables for configuration
2. Provide sensible defaults relative to the application base directory
3. Update this documentation with any new environment variables or requirements
4. Consider whether new functionality could be shared with the main parser
