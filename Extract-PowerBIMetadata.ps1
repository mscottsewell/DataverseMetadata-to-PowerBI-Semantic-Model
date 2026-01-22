param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectName = "ImaginationWorkshop",
    
    [Parameter(Mandatory=$false)]
    [string]$ExcelFileName = ""
)

# Build paths based on project name
$ProjectFolder = Join-Path "Reports" $ProjectName
$MetadataFolder = Join-Path $ProjectFolder "Metadata"
$PBIPFolder = Join-Path $ProjectFolder "PBIP"

# Auto-detect Excel file if not specified
if ([string]::IsNullOrWhiteSpace($ExcelFileName)) {
    $ExcelFileName = "$ProjectName Metadata Dictionary.xlsx"
}

$ExcelFilePath = Join-Path $MetadataFolder $ExcelFileName

# Check if project exists
if (-not (Test-Path $ProjectFolder)) {
    Write-Host "Project '$ProjectName' does not exist. Creating structure..." -ForegroundColor Yellow
    Write-Host ""
    
    # Create project folders
    New-Item -ItemType Directory -Path $MetadataFolder -Force | Out-Null
    New-Item -ItemType Directory -Path $PBIPFolder -Force | Out-Null
    
    Write-Host "✓ Created: $ProjectFolder" -ForegroundColor Green
    Write-Host "✓ Created: $MetadataFolder" -ForegroundColor Green
    Write-Host "✓ Created: $PBIPFolder" -ForegroundColor Green
    Write-Host ""
    Write-Host "NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "1. Add your Excel metadata file to: $MetadataFolder" -ForegroundColor Yellow
    Write-Host "   Expected filename: $ExcelFileName" -ForegroundColor Yellow
    Write-Host "2. Set sensitivity label to 'General' in the Excel file" -ForegroundColor Yellow
    Write-Host "3. Run this script again: .\Extract-PowerBIMetadata.ps1 -ProjectName '$ProjectName'" -ForegroundColor Yellow
    Write-Host ""
    exit 0
}

# Validate the Excel file exists
if (-not (Test-Path $ExcelFilePath)) {
    Write-Error "Excel file not found: $ExcelFilePath"
    Write-Host ""
    Write-Host "Please ensure:" -ForegroundColor Yellow
    Write-Host "1. The Excel file exists in: $MetadataFolder" -ForegroundColor Yellow
    Write-Host "2. The filename matches: $ExcelFileName" -ForegroundColor Yellow
    Write-Host "3. Sensitivity label is set to 'General'" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Get the base name for the output JSON file
$baseName = [System.IO.Path]::GetFileNameWithoutExtension($ExcelFilePath)
$directory = [System.IO.Path]::GetDirectoryName($ExcelFilePath)
if ([string]::IsNullOrEmpty($directory)) {
    $directory = Get-Location
}
$outputJsonPath = Join-Path $directory "$baseName.json"

Write-Host "Extracting metadata from: $ExcelFilePath" -ForegroundColor Cyan
Write-Host "Output will be saved to: $outputJsonPath" -ForegroundColor Cyan
Write-Host ""

# Create Python script content
$pythonScript = @'
import pandas as pd
import json
import sys
import os

def extract_metadata(excel_path):
    """Extract tables and fields from the Excel metadata file."""
    
    # Read the Entities list (first tab)
    entities_df = pd.read_excel(excel_path, sheet_name=0, skiprows=1)
    entities_df.columns = entities_df.iloc[0]
    entities_df = entities_df[1:].reset_index(drop=True)
    table_list = entities_df[['Entity', 'Schema Name']].dropna()
    
    # Read the Metadata tab
    metadata_df = pd.read_excel(excel_path, sheet_name='Metadata')
    fields_df = metadata_df[['Entity Logical Name', 'Schema Name', 'Display Name', 'Description']].dropna(subset=['Entity Logical Name', 'Schema Name', 'Display Name'])
    
    # Build the output structure
    output = {
        "metadata": {
            "source_file": os.path.basename(excel_path),
            "extraction_date": pd.Timestamp.now().isoformat(),
            "total_tables": len(table_list)
        },
        "tables": []
    }
    
    # For each table, extract its fields
    for idx, table_row in table_list.iterrows():
        table_display_name = table_row['Entity']
        table_schema_name = table_row['Schema Name']
        
        # Find all fields that belong to this table (case-insensitive match)
        table_fields = fields_df[fields_df['Entity Logical Name'].str.lower() == table_schema_name.lower()]
        
        fields_list = []
        for field_idx, field_row in table_fields.iterrows():
            field_info = {
                "displayName": field_row['Display Name'],
                "schemaName": field_row['Schema Name']
            }
            # Add description if it exists and is not NaN
            if pd.notna(field_row.get('Description')):
                field_info["description"] = field_row['Description']
            fields_list.append(field_info)
        
        table_info = {
            "displayName": table_display_name,
            "schemaName": table_schema_name,
            "fieldCount": len(fields_list),
            "fields": fields_list
        }
        
        output["tables"].append(table_info)
    
    return output

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python script.py <excel_file> <output_json>")
        sys.exit(1)
    
    excel_file = sys.argv[1]
    output_file = sys.argv[2]
    
    try:
        print(f"Reading Excel file: {excel_file}")
        result = extract_metadata(excel_file)
        
        print(f"Writing JSON output to: {output_file}")
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(result, f, indent=2, ensure_ascii=False)
        
        print(f"Successfully extracted {result['metadata']['total_tables']} tables")
        print(f"Total fields across all tables: {sum(t['fieldCount'] for t in result['tables'])}")
        
    except Exception as e:
        print(f"Error: {str(e)}", file=sys.stderr)
        sys.exit(1)
'@

# Write the Python script to a temporary file
$tempPythonScript = Join-Path $env:TEMP "extract_powerbi_metadata.py"
$pythonScript | Out-File -FilePath $tempPythonScript -Encoding UTF8

try {
    # Run the Python script
    $result = python $tempPythonScript $ExcelFilePath $outputJsonPath 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✓ Extraction completed successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Results:" -ForegroundColor Yellow
        $result | ForEach-Object { Write-Host "  $_" }
        Write-Host ""
        Write-Host "JSON file created: $outputJsonPath" -ForegroundColor Green
        
        # Display a summary
        $jsonData = Get-Content $outputJsonPath | ConvertFrom-Json
        Write-Host ""
        Write-Host "Summary:" -ForegroundColor Yellow
        Write-Host "  Total Tables: $($jsonData.metadata.total_tables)" -ForegroundColor White
        Write-Host "  Extraction Date: $($jsonData.metadata.extraction_date)" -ForegroundColor White
        Write-Host ""
        Write-Host "Tables:" -ForegroundColor Yellow
        foreach ($table in $jsonData.tables) {
            Write-Host "  - $($table.displayName): $($table.fieldCount) fields" -ForegroundColor White
        }
    }
    else {
        Write-Error "Python script failed with exit code: $LASTEXITCODE"
        Write-Host $result -ForegroundColor Red
        exit 1
    }
}
finally {
    # Clean up temporary file
    if (Test-Path $tempPythonScript) {
        Remove-Item $tempPythonScript -Force
    }
}
