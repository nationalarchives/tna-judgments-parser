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

| Variable | Description | Default |
|----------|-------------|---------|
| `COURT_METADATA_PATH` | Path to the CSV file containing court metadata | `{BaseDir}/court_metadata.csv` |
| `DATA_FOLDER_PATH` | Path to the folder containing judgment data files | `{BaseDir}` |
| `TRACKER_PATH` | Path to the CSV file tracking uploaded judgments | `{BaseDir}/uploaded-production.csv` |
| `OUTPUT_PATH` | Path where generated bundle files will be saved | `{BaseDir}` |
| `BULK_NUMBERS_PATH` | Path to the CSV file tracking bulk numbers | `{BaseDir}/bulk_numbers.csv` |
| `LAST_BEFORE_BATCH` | The last bulk number used before this batch started | `0` |
| `AWS_REGION` | AWS region for S3 bucket operations | Defaults to the region configured in AWS deployment environment |

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

The CSV file must contain the following columns (case-sensitive):

- `id` - Unique identifier for each judgment record
- `FilePath` - Path to the document file
- `Extension` - File extension (.pdf, .docx, .doc)
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
- `headnote_summary` - Summary of the judgment (included in metadata JSON but not in XML output)

**Note**: Column names are case-sensitive. If required columns are missing, the system will throw a validation error listing the missing columns.

### Tracker CSV

Tracks which judgments have been uploaded to production, preventing duplicate processing. This is particularly important for batch processing where:

- Multiple runs might be needed to process all files
- Some files might fail and need reprocessing
- Source files might be updated and need reprocessing

### Bulk Numbers CSV

Maintains a record of bulk number assignments for processed judgments. This is necessary because:

- Each judgment needs a unique identifier in the system
- Identifiers must be sequential within each batch
- We need to track which tribunal ID maps to which bulk number

Format:

```csv
bulk_num,trib_id
```

This tracking ensures consistency across multiple processing runs and helps maintain referential integrity between the source tribunal system and our system.

## Implementation Details

### Directory Structure

The expected directory structure for data processing is:

```plaintext
data/
├── tdr_metadata/
│   └── file-metadata.csv    # Maps judgment files to UUIDs
└── court_documents/         # Contains the actual judgment documents
    ├── {uuid}              # Original files (no extension)
    └── {uuid}.{ext}        # Processed files with extensions
```

### File Processing

The module implements robust file handling with several key features:

1. **UUID-based File Management**:
   - Files are stored internally using UUIDs
   - Original filenames are mapped to UUIDs in `file-metadata.csv`
   - Supports tracking of original file locations and names

2. **Extension Handling**:
   - Handles `.doc`, `.docx`, and `.pdf` files
   - Preserves original file extensions for tracking
   - Note: For `.doc` files, corresponding `.docx` files must be pre-converted and available in the court_documents directory

3. **Error Handling**:
   - Continues processing on individual file errors
   - Logs issues for manual review
   - Maintains processing state for partial batch completion

### Batch Processing

The module processes files in batches by ID, where each ID maps to one or more related judgment files. The process includes:

1. **Metadata Lookup**:
   - Files are found by looking up their ID in the court metadata CSV
   - Each ID may have multiple associated files

2. **UUID Resolution**:
   - Each judgment file's UUID is looked up in the TDR metadata
   - The UUID is used to locate the actual file in the court_documents directory

3. **Content Processing**:
   - Files are read and converted to the appropriate format
   - XML is generated for DOCX files
   - PDFs are wrapped in a simple XML structure

## Usage

To process backlog judgments:

1. Set up the environment variables:

    ```bash
    export COURT_METADATA_PATH=/path/to/metadata.csv
    export DATA_FOLDER_PATH=/path/to/data
    export TRACKER_PATH=/path/to/tracker.csv
    export OUTPUT_PATH=/path/to/output
    export BULK_NUMBERS_PATH=/path/to/bulk_numbers.csv
    export LAST_BEFORE_BATCH=0
    ```

2. Prepare your data directory structure as shown in the directory structure section above.

3. Run the backlog processor with an ID:

    ```bash
    backlog --id <id>
    ```

The program will:

- Look up the ID in the court metadata
- Process all associated judgment files
- Generate bundles and upload them to S3
- Track the processed files in the tracker CSV

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
