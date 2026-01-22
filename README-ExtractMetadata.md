# Extract-PowerBIMetadata.ps1 Usage

## Overview
This PowerShell script extracts table and field metadata from an Excel file within a project structure and outputs it to a JSON file with the same base name. The script organizes projects under the Reports/ folder, with each project containing Metadata/ and PBIP/ subfolders.

## Usage

```powershell
# Use default project (Dynamics 365 Sales)
.\Extract-PowerBIMetadata.ps1

# Specify a different project
.\Extract-PowerBIMetadata.ps1 -ProjectName "ProjectName"

# Specify custom Excel file name
.\Extract-PowerBIMetadata.ps1 -ProjectName "ProjectName" -ExcelFileName "CustomMetadata.xlsx"
```

## Examples

### Example 1: Use default project (Dynamics 365 Sales)

```powershell
.\Extract-PowerBIMetadata.ps1
```

### Example 2: Specify a different project

```powershell
.\Extract-PowerBIMetadata.ps1 -ProjectName "MyProject"
```

### Example 3: Specify custom Excel file name

```powershell
.\Extract-PowerBIMetadata.ps1 -ProjectName "MyProject" -ExcelFileName "CustomMetadata.xlsx"
```

## Output

The script creates a JSON file with the same name as the input Excel file.

**Input:** `Dynamics 365 Sales Metadata Dictionary.xlsx`  
**Output:** `Dynamics 365 Sales Metadata Dictionary.json`

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

## Parameters

- `ProjectName` (optional): Name of the project folder under Reports/. Default: `Dynamics 365 Sales`
- `ExcelFileName` (optional): Name of Excel file. Default: `{ProjectName} Metadata Dictionary.xlsx`

## Requirements

- PowerShell 5.1 or higher
- Python 3.x with pandas installed
- Excel file with:
  - First tab containing Entity and Schema Name columns
  - "Metadata" tab containing Entity Logical Name, Schema Name, Display Name, Attribute Type, and Description columns

## Notes

- The script automatically handles case-insensitive matching between entity names
- The output JSON uses UTF-8 encoding
- If the Excel file is not found, an error will be displayed
- The script creates a temporary Python file that is automatically cleaned up after execution
