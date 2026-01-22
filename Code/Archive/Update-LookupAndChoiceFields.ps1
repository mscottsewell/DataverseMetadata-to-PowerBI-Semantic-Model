# Script to update Lookup and Choice fields in TMDL files
param(
    [string]$MetadataFile = "ImaginationWorkshop Metadata Dictionary.xlsx",
    [string]$TmdlFolder = "CoreAI Imagination Workshop.SemanticModel\definition\tables"
)

Write-Host "Extracting metadata from Excel..." -ForegroundColor Cyan

# Read metadata using Python
$pythonScript = @"
import pandas as pd
import json

df = pd.read_excel('$MetadataFile', sheet_name='Metadata')

# Get Lookup fields
lookup = df[df['Attribute Type'] == 'Lookup'][['Entity Logical Name', 'Schema Name', 'Display Name', 'Description', 'Additional data']].fillna('')
lookup_dict = lookup.to_dict('records')

# Get Choice fields  
choice = df[df['Attribute Type'] == 'Choice'][['Entity Logical Name', 'Schema Name', 'Display Name', 'Description']].fillna('')
choice_dict = choice.to_dict('records')

result = {
    'lookup': lookup_dict,
    'choice': choice_dict
}

print(json.dumps(result))
"@

$metadata = python -c $pythonScript | ConvertFrom-Json

Write-Host "Found $($metadata.lookup.Count) Lookup fields and $($metadata.choice.Count) Choice fields" -ForegroundColor Green

# Map entity logical names to table file names
$tableNameMap = @{
    'account' = 'Account'
    'contact' = 'Contact'
    'caiiw_coreaiguide' = 'CoreAI Guide'
    'caiiw_eventattendee' = 'Event Attendee'
    'caiiw_imaginationworkshop' = 'Imagination Workshop'
    'caiiw_iwnextsteps' = 'IW Next Steps'
    'caiiw_iwproductfeedback' = 'IW Product Feedback'
    'caiiw_learningobjective' = 'Learning Objective'
    'caiiw_marketing' = 'Marketing'
    'caiiw_nomination' = 'Nomination'
    'caiiw_nominationlearningobjective' = 'Nomination Learning Objective'
    'caiiw_workshopfeedback' = 'Workshop Feedback'
    'systemuser' = 'User'
}

# Process each table
$updatedFiles = @()

foreach ($entity in $tableNameMap.Keys) {
    $tableName = $tableNameMap[$entity]
    $tmdlFile = Join-Path $TmdlFolder "$tableName.tmdl"
    
    if (-not (Test-Path $tmdlFile)) {
        Write-Host "  Skipping $tableName - file not found" -ForegroundColor Yellow
        continue
    }
    
    # Skip User table as requested
    if ($tableName -eq 'User') {
        Write-Host "  Skipping User table as requested" -ForegroundColor Yellow
        continue
    }
    
    Write-Host "Processing $tableName..." -ForegroundColor Cyan
    
    $content = Get-Content $tmdlFile -Raw
    $modified = $false
    
    # Process Choice fields - add "name" suffix to schema name
    $choiceFields = $metadata.choice | Where-Object { $_.'Entity Logical Name' -eq $entity }
    foreach ($field in $choiceFields) {
        $schemaName = $field.'Schema Name'
        $newSchemaName = $schemaName + 'name'
        
        # Update in SQL query (both SELECT and sourceColumn)
        if ($content -match "Base\.$schemaName\b") {
            Write-Host "  - Choice: $schemaName -> $newSchemaName" -ForegroundColor Green
            $content = $content -replace "Base\.$schemaName\b", "Base.$newSchemaName"
            $content = $content -replace "sourceColumn: $schemaName\b", "sourceColumn: $newSchemaName"
            $modified = $true
        }
    }
    
    # Process Lookup fields - update descriptions
    $lookupFields = $metadata.lookup | Where-Object { $_.'Entity Logical Name' -eq $entity }
    foreach ($field in $lookupFields) {
        $schemaName = $field.'Schema Name'
        $displayName = $field.'Display Name'
        $existingDesc = $field.'Description'
        $additionalData = $field.'Additional data'
        
        # Clean up additional data - remove line breaks and extra spaces
        if ($additionalData) {
            $additionalData = $additionalData -replace '_x000D_\r?\n', ' ' -replace '\s+', ' ' -replace '^\s+|\s+$', ''
        }
        
        # Build new description: "Display Name | Additional Data"
        if ($additionalData) {
            $newDescription = "$displayName | $additionalData"
        } else {
            $newDescription = $displayName
        }
        
        # Try to find column by display name (with quotes if it has spaces)
        $columnName = if ($displayName -match '\s') { "'$displayName'" } else { $displayName }
        $columnNameLower = $displayName.ToLower()
        
        # Check if column exists with display name or schema name
        if ($content -match "(?i)\s+column $columnName\s+") {
            Write-Host "  - Lookup: $displayName - updating description" -ForegroundColor Green
            
            # Remove any existing description comments before this column (may be multiple lines)
            $content = $content -replace "(?i)(\t)(///[^\r\n]*\r?\n)+(\t)(column $columnName)", "`$1`$4"
            
            # Add new description before the column definition
            $content = $content -replace "(?i)(\t)(column $columnName)", "`$1/// $newDescription`r`n`$1`$2"
            $modified = $true
        }
        elseif ($content -match "(?i)\s+column $schemaName\s+") {
            Write-Host "  - Lookup: $schemaName - updating description" -ForegroundColor Green
            
            # Remove any existing description comments before this column (may be multiple lines)
            $content = $content -replace "(?i)(\t)(///[^\r\n]*\r?\n)+(\t)(column $schemaName)", "`$1`$4"
            
            # Add new description before the column definition  
            $content = $content -replace "(?i)(\t)(column $schemaName)", "`$1/// $newDescription`r`n`$1`$2"
            $modified = $true
        }
    }
    
    if ($modified) {
        Set-Content -Path $tmdlFile -Value $content -NoNewline
        $updatedFiles += $tableName
        Write-Host "  Updated $tableName" -ForegroundColor Green
    }
}

Write-Host "`nUpdated $($updatedFiles.Count) files:" -ForegroundColor Cyan
$updatedFiles | ForEach-Object { Write-Host "  - $_" -ForegroundColor Green }
