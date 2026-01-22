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
$isNewProject = $false
if (-not (Test-Path $ProjectFolder)) {
    $isNewProject = $true
    Write-Host "Project '$ProjectName' does not exist. Creating structure..." -ForegroundColor Yellow
    Write-Host ""
    
    # Create project folders
    New-Item -ItemType Directory -Path $MetadataFolder -Force | Out-Null
    New-Item -ItemType Directory -Path $PBIPFolder -Force | Out-Null
        # Create blank DataverseURL.txt placeholder
    $dataverseUrlFile = Join-Path $MetadataFolder "DataverseURL.txt"
    "" | Out-File -FilePath $dataverseUrlFile -Encoding UTF8
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
    fields_df = metadata_df[['Entity Logical Name', 'Schema Name', 'Display Name', 'Attribute Type', 'Description']].dropna(subset=['Entity Logical Name', 'Schema Name', 'Display Name'])
    
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
            # Add attribute type if it exists
            if pd.notna(field_row.get('Attribute Type')):
                field_info["attributeType"] = field_row['Attribute Type']
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
        
        # Check if PBIP files need to be created
        $pbipFilePath = Join-Path $PBIPFolder "$ProjectName.pbip"
        if (-not (Test-Path $pbipFilePath)) {
            Write-Host ""
            Write-Host "Setting up Power BI project files..." -ForegroundColor Cyan
            Write-Host ""
            
            # Check for DataverseURL.txt file first
            $dataverseUrlFile = Join-Path $MetadataFolder "DataverseURL.txt"
            $dataverseUrl = ""
            
            if (Test-Path $dataverseUrlFile) {
                $fileContent = Get-Content $dataverseUrlFile -Raw
                if (-not [string]::IsNullOrWhiteSpace($fileContent)) {
                    $dataverseUrl = $fileContent.Trim()
                    Write-Host "✓ Using Dataverse URL from DataverseURL.txt: $dataverseUrl" -ForegroundColor Green
                }
            }
            
            # Prompt for Dataverse URL if not found in file
            if ([string]::IsNullOrWhiteSpace($dataverseUrl)) {
                Write-Host "To avoid connection errors, please provide your Dataverse URL:" -ForegroundColor Yellow
                Write-Host "(Tip: Save it to '$dataverseUrlFile' to skip this prompt next time)" -ForegroundColor Gray
                Write-Host "Example: myorg.crm.dynamics.com" -ForegroundColor Gray
                $dataverseUrl = Read-Host "Enter Dataverse URL (or press Enter to use default 'mydataverseURL.crm.dynamics.com')"
                if ([string]::IsNullOrWhiteSpace($dataverseUrl)) {
                    $dataverseUrl = "mydataverseURL.crm.dynamics.com"
                    Write-Host "Using default URL: $dataverseUrl" -ForegroundColor Gray
                }
                else {
                    Write-Host "Using URL: $dataverseUrl" -ForegroundColor Green
                    # Save to file for next time
                    $dataverseUrl | Out-File -FilePath $dataverseUrlFile -Encoding UTF8 -NoNewline
                    Write-Host "✓ Saved to DataverseURL.txt for future use" -ForegroundColor Green
                }
            }
            Write-Host ""
            
            # Copy template
            $templatePath = "Code\PBIP_DefaultTemplate"
            if (Test-Path $templatePath) {
                Write-Host "  Copying template files..." -ForegroundColor White
                Copy-Item -Path "$templatePath\*" -Destination $PBIPFolder -Recurse -Force
                
                # Rename template files
                Write-Host "  Renaming files to match project..." -ForegroundColor White
                Rename-Item -Path (Join-Path $PBIPFolder "Template.pbip") -NewName "$ProjectName.pbip"
                Rename-Item -Path (Join-Path $PBIPFolder "Template.Report") -NewName "$ProjectName.Report"
                Rename-Item -Path (Join-Path $PBIPFolder "Template.SemanticModel") -NewName "$ProjectName.SemanticModel"
                
                # Update PBIP file references
                Write-Host "  Updating internal references..." -ForegroundColor White
                $pbipContent = Get-Content $pbipFilePath -Raw
                $pbipContent = $pbipContent -replace 'Template\.Report', "$ProjectName.Report"
                $pbipContent | Set-Content $pbipFilePath -NoNewline
                
                # Update report definition
                $reportDefPath = Join-Path $PBIPFolder "$ProjectName.Report\definition.pbir"
                $reportDefContent = Get-Content $reportDefPath -Raw
                $reportDefContent = $reportDefContent -replace 'Template\.SemanticModel', "$ProjectName.SemanticModel"
                $reportDefContent | Set-Content $reportDefPath -NoNewline
                
                # Update Dataverse URL in expressions.tmdl
                $expressionsPath = Join-Path $PBIPFolder "$ProjectName.SemanticModel\definition\expressions.tmdl"
                $expressionsContent = Get-Content $expressionsPath -Raw
                $expressionsContent = $expressionsContent -replace 'mydataverseURL\.crm\.dynamics\.com', $dataverseUrl
                $expressionsContent | Set-Content $expressionsPath -NoNewline
                
                # Update model.tmdl with table references
                Write-Host "  Adding table definitions..." -ForegroundColor White
                $modelPath = Join-Path $PBIPFolder "$ProjectName.SemanticModel\definition\model.tmdl"
                $tableNames = $jsonData.tables | ForEach-Object { $_.schemaName }
                $queryOrder = @("DataverseURL") + $tableNames
                $queryOrderStr = $queryOrder | ForEach-Object { "`"$_`"" }
                $queryOrderLine = "annotation PBI_QueryOrder = [$($queryOrderStr -join ',')]"
                
                $refLines = $tableNames | ForEach-Object {
                    if ($_ -match '\s') {
                        "ref table '$_'"
                    } else {
                        "ref table $_"
                    }
                }
                
                $modelContent = Get-Content $modelPath -Raw
                $modelContent = $modelContent -replace 'annotation PBI_QueryOrder = \["DataverseURL"\]', $queryOrderLine
                $modelContent = $modelContent -replace '(annotation PBI_ProTooling[^\r\n]+)', "`$1`n`n$($refLines -join "`n")`n"
                $modelContent | Set-Content $modelPath -NoNewline
                
                # Create table definition files
                $tablesDir = Join-Path $PBIPFolder "$ProjectName.SemanticModel\definition\tables"
                New-Item -ItemType Directory -Path $tablesDir -Force | Out-Null
                
                foreach ($table in $jsonData.tables) {
                    $tableName = $table.schemaName
                    $displayName = $table.displayName
                    $tableFile = Join-Path $tablesDir "$tableName.tmdl"
                    
                    # Generate GUID tags
                    $tableTag = [guid]::NewGuid().ToString()
                    
                    # Create columns for ALL fields
                    $columnDefs = @()
                    $sqlColumns = @()
                    
                    # First, add the primary ID column (derived from table name)
                    $primaryIdField = $tableName.ToLower() -replace '\s', '' # Remove spaces, lowercase
                    $primaryIdField = $primaryIdField + "id"
                    $idColTag = [guid]::NewGuid().ToString()
                    
                    $idColumnDef = @"
`tcolumn $primaryIdField
`t`tdataType: string
`t`tisHidden
`t`tlineageTag: $idColTag
`t`tsummarizeBy: none
`t`tsourceColumn: $primaryIdField

`t`tannotation SummarizationSetBy = Automatic
"@
                    $columnDefs += $idColumnDef
                    $sqlColumns += "        ,Base.$primaryIdField"
                    
                    # Then add all other fields from metadata
                    foreach ($field in $table.fields) {
                        $colTag = [guid]::NewGuid().ToString()
                        $fieldName = $field.schemaName
                        $fieldDisplay = $field.displayName
                        $attributeType = if ($field.attributeType) { $field.attributeType } else { "" }
                        
                        # Determine data type based on attribute type
                        $dataType = switch ($attributeType) {
                            "Currency" { "decimal" }
                            "Decimal" { "double" }
                            "Whole Number" { "int64" }
                            "Integer" { "int64" }
                            default { "string" }
                        }
                        
                        # Determine format string based on attribute type
                        $formatString = switch ($attributeType) {
                            "Currency" { "`n`t`tformatString: \`$#,0.00;(\`$#,0.00);\`$#,0.00" }
                            "Decimal" { "`n`t`tformatString: #,0.00" }
                            default { "" }
                        }
                        
                        # For Lookup, Customer, and Owner fields, add BOTH the GUID column (hidden) and the Name column (visible)
                        if ($attributeType -eq "Lookup" -or $attributeType -eq "Customer" -or $attributeType -eq "Owner") {
                            # Hidden GUID column
                            $guidColTag = [guid]::NewGuid().ToString()
                            $guidColumnDef = @"
`tcolumn $fieldName
`t`tdataType: string
`t`tisHidden
`t`tlineageTag: $guidColTag
`t`tsummarizeBy: none
`t`tsourceColumn: $fieldName

`t`tannotation SummarizationSetBy = Automatic
"@
                            $columnDefs += $guidColumnDef
                            $sqlColumns += "        ,Base.$fieldName"
                            
                            # Visible Name column with friendly display name
                            $nameColTag = [guid]::NewGuid().ToString()
                            $nameFieldName = $fieldName + "name"
                            $nameColumnDef = @"
`tcolumn '$fieldDisplay'
`t`tdataType: string
`t`tlineageTag: $nameColTag
`t`tsummarizeBy: none
`t`tsourceColumn: $nameFieldName

`t`tannotation SummarizationSetBy = Automatic
"@
                            $columnDefs += $nameColumnDef
                            $sqlColumns += "        ,Base.$nameFieldName"
                        }
                        # For Choice, Picklist, and Two Options fields, only include the Name column (readable label)
                        elseif ($attributeType -eq "Choice" -or $attributeType -eq "Picklist" -or $attributeType -eq "Two Options") {
                            $nameFieldName = $fieldName + "name"
                            $choiceColumnDef = @"
`tcolumn '$fieldDisplay'
`t`tdataType: string
`t`tlineageTag: $colTag
`t`tsummarizeBy: none
`t`tsourceColumn: $nameFieldName

`t`tannotation SummarizationSetBy = User
"@
                            $columnDefs += $choiceColumnDef
                            $sqlColumns += "        ,Base.$nameFieldName"
                        }
                        # For all other field types, standard column
                        else {
                            # Determine if column should be hidden (ID columns)
                            $isHidden = if ($fieldName -match 'id$|^_.*_value$') { "`n`t`tisHidden" } else { "" }
                            
                            # Add column definition
                            $columnDef = @"
`tcolumn '$fieldDisplay'
`t`tdataType: $dataType$formatString
`t`tlineageTag: $colTag
`t`tsummarizeBy: none
`t`tsourceColumn: $fieldName$isHidden

`t`tannotation SummarizationSetBy = Automatic
"@
                            $columnDefs += $columnDef
                            
                            # Add to SQL SELECT list
                            $sqlColumns += "        ,Base.$fieldName"
                        }
                    }
                    
                    # Build SQL query with proper formatting
                    $sqlSelect = $sqlColumns -join "`n"
                    # Remove leading comma from first column and add space after SELECT
                    $sqlSelect = $sqlSelect -replace '^\s+,', '    SELECT '
                    
                    # Escape backticks in the query for PowerShell here-string
                    $tableNameQuoted = if ($tableName -match '\s') { "'$tableName'" } else { $tableName }
                    
                    $tableContent = @"
table $tableNameQuoted
`tlineageTag: $tableTag

$($columnDefs -join "`n`n")

`tpartition $tableNameQuoted = m
`t`tmode: directQuery
`t`tsource = ``````
`t`t`t`tlet
    Dataverse = CommonDataService.Database(DataverseURL,[CreateNavigationProperties=false]),
    Source = Value.NativeQuery(Dataverse,"
`t`t`t`t
$sqlSelect
    FROM $tableName as Base
    
    " ,null ,[EnableFolding=true])
in
    Source
`t`t`t`t``````

`tannotation PBI_NavigationStepName = Navigation

`tannotation PBI_ResultType = Table


"@
                    $tableContent | Set-Content $tableFile -NoNewline
                }
                
                Write-Host ""
                Write-Host "✓ Power BI project files created successfully!" -ForegroundColor Green
                Write-Host "  Location: $pbipFilePath" -ForegroundColor White
                Write-Host "  Tables created: $($jsonData.tables.Count)" -ForegroundColor White
                Write-Host "  Total columns: $($jsonData.tables | ForEach-Object { $_.fieldCount } | Measure-Object -Sum | Select-Object -ExpandProperty Sum)" -ForegroundColor White
                Write-Host "  Mode: DirectQuery with native SQL" -ForegroundColor White
                Write-Host "  Dataverse URL: $dataverseUrl" -ForegroundColor White
                Write-Host ""
                Write-Host "NEXT STEPS:" -ForegroundColor Cyan
                Write-Host "1. Open $ProjectName.pbip in Power BI Desktop" -ForegroundColor Yellow
                Write-Host "2. Connect to your Dataverse environment and test the queries" -ForegroundColor Yellow
                Write-Host "3. Define relationships between tables in the model" -ForegroundColor Yellow
            }
            else {
                Write-Host ""
                Write-Host "Warning: Template not found at $templatePath" -ForegroundColor Yellow
                Write-Host "PBIP files must be created manually" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host ""
            Write-Host "Note: PBIP files already exist at: $pbipFilePath" -ForegroundColor Cyan
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
