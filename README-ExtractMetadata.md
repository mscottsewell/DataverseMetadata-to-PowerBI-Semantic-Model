# Extract-PowerBIMetadata.ps1 Usage

## Overview
This PowerShell script extracts table and field metadata from an Excel file and outputs it to a JSON file with the same base name.

## Usage

```powershell
.\Extract-PowerBIMetadata.ps1 -ExcelFilePath "<path-to-excel-file>"
```

## Examples

### Example 1: Extract from file in current directory
```powershell
.\Extract-PowerBIMetadata.ps1 -ExcelFilePath "ImaginationWorkshop Metadata Dictionary.xlsx"
```

### Example 2: Extract from file with full path
```powershell
.\Extract-PowerBIMetadata.ps1 -ExcelFilePath "C:\Data\MyMetadata.xlsx"
```

### Example 3: Using relative path
```powershell
.\Extract-PowerBIMetadata.ps1 -ExcelFilePath "..\data\metadata.xlsx"
```

## Output

The script creates a JSON file with the same name as the input Excel file.

**Input:** `ImaginationWorkshop Metadata Dictionary.xlsx`  
**Output:** `ImaginationWorkshop Metadata Dictionary.json`

## JSON Structure

```json
{
  "metadata": {
    "source_file": "filename.xlsx",
    "extraction_date": "2026-01-21T21:46:40.276628",
    "total_tables": 13
  },
  "tables": [
    {
      "displayName": "Account",
      "schemaName": "Account",
      "fieldCount": 8,
      "fields": [
        {
          "displayName": "Address 1",
          "schemaName": "Address1_Composite"
        },
        ...
      ]
    },
    ...
  ]
}
```

## Requirements

- PowerShell 5.1 or higher
- Python 3.x with pandas installed
- Excel file with:
  - First tab containing Entity and Schema Name columns
  - "Metadata" tab containing Entity Logical Name, Schema Name, and Display Name columns

## Notes

- The script automatically handles case-insensitive matching between entity names
- The output JSON uses UTF-8 encoding
- If the Excel file is not found, an error will be displayed
- The script creates a temporary Python file that is automatically cleaned up after execution
