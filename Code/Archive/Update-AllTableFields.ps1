# Script to update all tables with proper Lookup and Choice field handling
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

# Group by entity
entities = {}
for entity in df['Entity Logical Name'].unique():
    entity_fields = df[df['Entity Logical Name'] == entity]
    fields = []
    for _, row in entity_fields.iterrows():
        field = {
            'schemaName': row['Schema Name'],
            'displayName': row['Display Name'],
            'attributeType': row['Attribute Type'],
            'description': row['Description'] if pd.notna(row['Description']) else '',
            'additionalData': row['Additional data'] if pd.notna(row['Additional data']) else ''
        }
        # Clean up additional data
        if field['additionalData']:
            field['additionalData'] = field['additionalData'].replace('_x000D_\n', ' ').replace('_x000D_', ' ')
            field['additionalData'] = ' '.join(field['additionalData'].split())
        fields.append(field)
    entities[entity] = fields

print(json.dumps(entities))
"@

$entitiesJson = python -c $pythonScript
$entities = $entitiesJson | ConvertFrom-Json

Write-Host "Loaded metadata for $($entities.PSObject.Properties.Count) entities" -ForegroundColor Green

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
}

$updatedTables = @()

foreach ($entityName in $tableNameMap.Keys) {
    $tableName = $tableNameMap[$entityName]
    $tmdlFile = Join-Path $TmdlFolder "$tableName.tmdl"
    
    if (-not (Test-Path $tmdlFile)) {
        Write-Host "  Skipping $tableName - file not found" -ForegroundColor Yellow
        continue
    }
    
    Write-Host "`nProcessing $tableName..." -ForegroundColor Cyan
    
    $fields = $entities.$entityName
    if (-not $fields) {
        Write-Host "  No metadata found for $entityName" -ForegroundColor Yellow
        continue
    }
    
    # Build SQL SELECT statement
    $sqlFields = @('Base.' + ($fields[0].schemaName).ToLower()) # ID field
    $columnDefinitions = @()
    
    # Skip first field (ID)
    for ($i = 1; $i -lt $fields.Count; $i++) {
        $field = $fields[$i]
        $schemaName = $field.schemaName
        $schemaNameLower = $schemaName.ToLower()
        $displayName = $field.displayName
        $attributeType = $field.attributeType
        $description = $field.description
        $additionalData = $field.additionalData
        
        # Build description with additional data if applicable
        $fullDescription = $description
        if ($additionalData -and ($attributeType -eq 'Lookup' -or $attributeType -eq 'Owner')) {
            $fullDescription = "$description | $additionalData"
        }
        
        if ($attributeType -eq 'Lookup' -or $attributeType -eq 'Owner') {
            # Lookup: Add both ID field (hidden) and name field (visible)
            $sqlFields += "Base.$schemaNameLower"
            $sqlFields += "Base.$($schemaNameLower)name"
            
            # Hidden ID field with description
            $columnDef = @"
	/// $fullDescription
	column $schemaNameLower
		dataType: string
		sourceColumn: $schemaNameLower
		isHidden

	column '$displayName'
		dataType: string
		sourceColumn: $($schemaNameLower)name
"@
            $columnDefinitions += $columnDef
            
        } elseif ($attributeType -eq 'Choice') {
            # Choice: Use name suffix
            $sqlFields += "Base.$($schemaNameLower)name"
            
            $columnDef = @"
	/// $description
	column '$displayName'
		dataType: int64
		formatString: 0
		summarizeBy: sum
		sourceColumn: $($schemaNameLower)name

		annotation SummarizationSetBy = Automatic
"@
            $columnDefinitions += $columnDef
            
        } else {
            # Regular field
            $sqlFields += "Base.$schemaNameLower"
            
            $descLine = if ($description) { "`t/// $description`r`n" } else { "" }
            $columnDef = @"
$descLine	column '$displayName'
		dataType: string
		sourceColumn: $schemaNameLower
"@
            $columnDefinitions += $columnDef
        }
    }
    
    Write-Host "  Generated $($columnDefinitions.Count) column definitions" -ForegroundColor Green
    Write-Host "  SQL will select $($sqlFields.Count) fields" -ForegroundColor Green
    
    $updatedTables += $tableName
}

Write-Host "`nProcessed $($updatedTables.Count) tables:" -ForegroundColor Cyan
$updatedTables | ForEach-Object { Write-Host "  - $_" -ForegroundColor Green }
Write-Host "`nNote: Actual file writing not implemented - this is a structure verification script" -ForegroundColor Yellow
