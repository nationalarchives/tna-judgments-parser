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

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `COURT_METADATA_PATH` | Path to the CSV file containing court metadata | `{BaseDir}/court_metadata.csv` |
| `DATA_FOLDER_PATH` | Path to the folder containing judgment data files | `{BaseDir}` |
| `TRACKER_PATH` | Path to the CSV file tracking uploaded judgments | `{BaseDir}/uploaded-production.csv` |
| `OUTPUT_PATH` | Path where generated bundle files will be saved | `{BaseDir}` |
| `BULK_NUMBERS_PATH` | Path to the CSV file tracking bulk numbers | `{BaseDir}/bulk_numbers.csv` |
| `LAST_BEFORE_BATCH` | The last bulk number used before this batch started | `0` |

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
   - Automatic conversion of `.doc` to `.docx` format
   - Preserves original file extensions for tracking
   - Maintains both original and processed versions

3. **Error Handling**:
   - Continues processing on individual file errors
   - Logs issues for manual review
   - Maintains processing state for partial batch completion

### Batch Processing

The module processes files in batches with these key components:

1. **File Discovery**:

   ```csharp
   var files = Files.GetFiles(dataDir, pattern);
   ```

2. **Extension Processing**:

   ```csharp
   Files.CopyAllFilesWithExtension(dataDir, files);
   ```

3. **Content Reading**:

   ```csharp
   byte[] content = Files.ReadFile(dataDir, metadataLine);
   ```

## Usage

To process backlog judgments:

```bash
export COURT_METADATA_PATH=/path/to/metadata.csv
export DATA_FOLDER_PATH=/path/to/data
export TRACKER_PATH=/path/to/tracker.csv
export OUTPUT_PATH=/path/to/output
export BULK_NUMBERS_PATH=/path/to/bulk_numbers.csv
export LAST_BEFORE_BATCH=0
```

Prepare your data directory structure as shown in the directory structure section above, then run the backlog processor:

```csharp
// Example
uint id = 2;
bool autoPublish = true;

var helper = new Helper
{
    PathToCourtMetadataFile = Environment.GetEnvironmentVariable("COURT_METADATA_PATH"),
    PathToDataFolder = Environment.GetEnvironmentVariable("DATA_FOLDER_PATH")
};
```

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
