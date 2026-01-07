# TNA Judgments Parser - Backlog Module

This module provides a specialized entry point to the parser specifically designed for processing historic tribunal judgments. It handles the unique challenges of importing judgments from legacy tribunal systems, where the source materials and metadata come in various formats.

<!-- TOC -->
* [TNA Judgments Parser - Backlog Module](#tna-judgments-parser---backlog-module)
  * [Overview](#overview)
    * [Purpose](#purpose)
    * [Metadata Handling](#metadata-handling)
    * [Future Development](#future-development)
  * [Get Started](#get-started)
    * [Pre-requisites](#pre-requisites)
    * [Setup and Execution](#setup-and-execution)
    * [Processing Modes](#processing-modes)
  * [Configuration](#configuration)
    * [File Formats](#file-formats)
    * [Directory Structure](#directory-structure)
    * [Court Metadata CSV](#court-metadata-csv)
      * [Required Columns](#required-columns)
      * [Optional Columns](#optional-columns)
      * [CSV Line Example](#csv-line-example)
    * [Tracker CSV](#tracker-csv)
    * [Environment Variables](#environment-variables)
      * [AWS Configuration](#aws-configuration)
  * [Development](#development)
    * [Components](#components)
    * [Relationship with Main Parser](#relationship-with-main-parser)
    * [Development Guidelines](#development-guidelines)
<!-- TOC -->

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

## Get Started

The backlog parser must be run locally to process a new batch.

### Pre-requisites

- .NET 8 SDK

### Setup and Execution

1. **Prepare data directory structure** as shown in the [Directory Structure](#directory-structure) section.
2. **Set environment variables** shown in [Environment Variables](#environment-variables) section by either:
    - Exporting in the shell (e.g. `export MY_VAR=value`)
    - Adding them to a `.env` file in the assembly folder
    - Adding them to the build/run configuration in your IDE (e.g. [Run configs in Rider](https://www.jetbrains.com/help/rider/Run_Debug_Configuration.html#envvars-progargs))
3. **Run the processor** using one of the [Processing Modes](#processing-modes).
    - It is recommended to do a dry run before uploading to S3 to ensure that there are no hidden complications in the new data.

### Processing Modes

The backlog processor supports three modes of operation:

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

3. **Dry run**:

   ```bash
   dotnet run --dry-run
   dotnet run --dry-run --id <id>
   ```

   Processes every record or specific record (as specified by `--id`), writes the files to the output path but does not send the results to S3

## Configuration

### File Formats

The backlog parser supports `PDF` and `DOCX` file formats. 

**Other files must first be converted into one of these formats before going through the parser.**

### Directory Structure

The expected directory structure for data processing is:

```plaintext
data/
├── tdr_metadata/
│   └── file-metadata.csv              # Maps original judgment filenames to UUIDs (only required if UUID isn't provided in the Court Metadata CSV)
└── court_documents/                   # Contains the actual judgment documents
    ├── {uuid}                         # UUID-named files without extensions
    ├── {uuid}                         # (e.g., a1b2c3d4-e5f6-7890-abcd-ef1234567890)
    └── {uuid}                         # All files stored using UUID as filename
```

**Important Notes on File Naming:**

- All files in `court_documents/` are named using only their UUID (no file extension)
- The file extension information is stored in the court metadata CSV for reference
- The parser determines file type processing based on the `Extension` field in the metadata, not from the actual filename


### Court Metadata CSV

Contains metadata extracted from tribunal-specific spreadsheets about courts and their judgments. This data includes:

- Court identifiers and names
- Judgment dates
- Case numbers
- Document metadata from the source tribunal system
- Any tribunal-specific metadata fields

#### Required Columns

The CSV file must contain the following columns for each judgment:

- `id` - Unique identifier for each judgment record
- `court` - Court code that maps to a Court object (e.g., "EWHC-QBD-Admin", "UKFTT-GRC"). Must match a valid court code defined in the Courts.ByCode dictionary
- `FilePath` - Path to the judgment file relative to the base directory (UUID without extension)
- `Extension` - File extension indicating the original file type (.pdf, .docx, .doc)
- `decision_datetime` - Date and time when the decision was made (format: "yyyy-MM-dd HH:mm:ss")
- `CaseNo` - Case number(s) (with space inbetween if multiple)
- `claimants` OR `appellants` - Name(s) of the claimant(s)/appellant(s)
- `respondent` - Name(s) of the respondent(s)

#### Optional Columns

The following columns are optional:

- `main_category` - Primary category name
- `main_subcategory` - Primary subcategory name (child of main_category)
- `sec_category` - Secondary category name (optional)
- `sec_subcategory` - Secondary subcategory name (child of sec_category, only used if sec_category is provided)
- `ncn` - Neutral Citation Number (NCN) for the judgment, when available. If provided, this appears as `uk:cite` in the generated AkomaNtoso XML
- `headnote_summary` - Summary of the judgment (included in metadata JSON but not in XML output)
- `Jurisdictions` - Jurisdictions to be added as `uk:jurisdiction` elements in the xml. This can be blank, a single item or a comma seperated list in quotes (e.g. `"jurisdiction1,jurisdiction2"`)
- `uuid` - The TDR-cleansed filenames. If not provided then it will be derived from `tdr_metadata/file-metadata.csv`
- `webarchiving` - Link to the webarchive for this judgment

**Note**: If required columns are missing, the system will throw a validation error listing the missing columns.

#### CSV Line Example

```csv
id,court,FilePath,Extension,decision_datetime,CaseNo,claimants,respondent,main_category,main_subcategory
123,UKFTT-GRC,a1b2c3d4-e5f6-7890-abcd-ef1234567890,.pdf,2025-01-15 09:00:00,GRC/2025/001,Smith,Secretary of State,Immigration,Appeal Rights
124,EWHC-QBD-Admin,b2c3d4e5-f6g7-8901-bcde-f23456789012,.docx,2025-01-16 10:00:00,IA/2025/002,Jones,HMRC,Tax,VAT Appeals
125,UKUT-IAC,c3d4e5f6-g7h8-9012-cdef-34567890123a,.doc,2025-01-17 11:00:00,UKUT/2025/003,Williams,Home Office,Immigration,Entry Clearance
```

- Line 1 = ID 123 (Smith vs Secretary of State)
- Line 2 = ID 124 (Jones vs HMRC)
- Line 3 = ID 125 (Williams vs Home Office)

### Tracker CSV

This is created and populated by the Backlog Parser. It tracks which judgments have been uploaded to production, preventing duplicate processing. This is particularly important for batch processing where:

- Multiple runs might be needed to process all files
- Some files might fail and need reprocessing
- Source files might be updated and need reprocessing

### Environment Variables

The module uses environment variables for configuration. All paths default to the application's base directory if not specified.

| Variable              | Description                                                                                            | Default                                                         |
|-----------------------|--------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------|
| `COURT_METADATA_PATH` | Path to the CSV file containing court metadata                                                         | `{BaseDir}/court_metadata.csv`                                  |
| `DATA_FOLDER_PATH`    | Path to the folder containing judgment data files                                                      | `{BaseDir}`                                                     |
| `TRACKER_PATH`        | Path to the CSV file tracking uploaded judgments                                                       | `{BaseDir}/uploaded-production.csv`                             |
| `OUTPUT_PATH`         | Path where generated bundle files will be saved                                                        | `{BaseDir}`                                                     |
| `JUDGMENTS_FILE_PATH` | The filepath prefix used in the court metadata csv (used to crossference with file-metadata.csv paths) | `""`                                                            |
| `HMCTS_FILES_PATH`    | The filepath prefix used in the file-metadata csv (used to crossference with court metadata csv paths) | `""`                                                            |
| `AWS_REGION`          | AWS region for S3 bucket operations                                                                    | Defaults to the region configured in AWS deployment environment |
| `BUCKET_NAME`         | AWS bucket to upload processed files and xml to                                                        | None - must be set if not using dry run mode                    |

where `{BaseDir}` is where the application's assemblies are (usually `backlog/bin/Debug/net8.0` when running locally)

#### AWS Configuration

When not in dry run mode, the Backlog Parser requires access to an S3 bucket for storing processed judgments.
To determine the `AWS_REGION` the AWS SDK for .NET will use:

1. The region from the `AWS_REGION` environment variable if set
2. The region configured in the AWS deployment environment (EC2, Lambda, ECS, etc.)
3. The region configured in your local AWS CLI configuration

## Development

### Components

The entry point for the application is the `Main` method in `backlog/src/Program.cs`. Any commandline arguments are passed through to the `args` parameter by the .NET runtime.

### Relationship with Main Parser

This module exists as a separate entry point from the main parser ([see main README](../README.md)) but uses the main parser API

1. **Different Input Processing**:
   - Main parser: Expects single DOCX files with optional attachments
   - Backlog module: Handles batches with mixed formats (PDF, DOC, DOCX) and external metadata

2. **Metadata Handling**:
   - Backlog module: from external spreadsheets

### Development Guidelines

When adding new features or modifying paths, ensure to:

1. Use environment variables for configuration
2. Provide sensible defaults relative to the application base directory
3. Update this documentation with any new environment variables or requirements
4. Consider whether new functionality could be shared with the main parser
