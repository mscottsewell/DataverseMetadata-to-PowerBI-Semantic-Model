# Load the metadata JSON
$metadata = Get-Content "ImaginationWorkshop Metadata Dictionary.json" -Raw | ConvertFrom-Json

# Get all tables except Account
$tablesToAdd = $metadata.tables | Where-Object { $_.displayName -ne "Account" }

Write-Host "Preparing to add $($tablesToAdd.Count) tables..." -ForegroundColor Cyan
Write-Host ""

$tableDefinitions = @()

foreach ($table in $tablesToAdd) {
    $displayName = $table.displayName
    $schemaName = $table.schemaName
    $primaryKeyId = ($schemaName.ToLower() + "id")
    
    Write-Host "Building definition for: $displayName" -ForegroundColor Yellow
    
    # Read the Power Query file
    $pqFileName = $displayName -replace '[^\w\s-]', '' -replace '\s+', '_'
    $pqFile = "PowerQuery\$pqFileName.pq"
    
    if (-not (Test-Path $pqFile)) {
        Write-Host "  ✗ Power Query file not found: $pqFile" -ForegroundColor Red
        continue
    }
    
    $mExpression = (Get-Content $pqFile -Raw).Trim()
    
    # Build columns array
    $columns = @()
    
    # Add primary key column
    $columns += @{
        name = $primaryKeyId
        sourceColumn = $primaryKeyId
        dataType = "String"
    }
    
    # Add all other columns
    foreach ($field in $table.fields) {
        $column = @{
            name = $field.displayName
            sourceColumn = $field.schemaName.ToLower()
            dataType = "String"
        }
        
        if ($field.description) {
            $column.description = $field.description
        }
        
        $columns += $column
    }
    
    # Create table definition
    $tableDefinition = @{
        name = $displayName
        partitionName = $displayName
        mExpression = $mExpression
        columns = $columns
    }
    
    $tableDefinitions += $tableDefinition
    
    Write-Host "  ✓ Definition created with $($columns.Count) columns" -ForegroundColor Green
}

# Save all definitions to a JSON file
$output = @{
    definitions = $tableDefinitions
    operation = "Create"
}

$outputJson = $output | ConvertTo-Json -Depth 10
$outputJson | Out-File "tables_to_add.json" -Encoding UTF8

Write-Host ""
Write-Host "✓ All table definitions saved to: tables_to_add.json" -ForegroundColor Green
Write-Host "Total tables to add: $($tableDefinitions.Count)" -ForegroundColor Cyan
