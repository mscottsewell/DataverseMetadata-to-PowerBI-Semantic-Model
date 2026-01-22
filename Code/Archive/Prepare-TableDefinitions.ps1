# Load the metadata JSON
$json = Get-Content "ImaginationWorkshop Metadata Dictionary.json" | ConvertFrom-Json

# Get all tables except Account (which already exists)
$tablesToAdd = $json.tables | Where-Object { $_.displayName -ne "Account" }

Write-Host "Adding $($tablesToAdd.Count) tables to the Power BI model..." -ForegroundColor Cyan
Write-Host ""

foreach ($table in $tablesToAdd) {
    $displayName = $table.displayName
    $schemaName = $table.schemaName
    $primaryKeyId = $schemaName.ToLower() + "id"
    
    Write-Host "Processing: $displayName" -ForegroundColor Yellow
    
    # Read the Power Query file
    $pqFileName = $displayName -replace '[^\w\s-]', '' -replace '\s+', '_'
    $pqFile = "PowerQuery\$pqFileName.pq"
    
    if (Test-Path $pqFile) {
        $pqContent = Get-Content $pqFile -Raw
        
        # Build column definitions for the table
        $columns = @()
        
        # Add the primary key column first
        $columns += @{
            name = $primaryKeyId
            sourceColumn = $primaryKeyId
            dataType = "string"
        }
        
        # Add all other columns with display names and descriptions
        foreach ($field in $table.fields) {
            $column = @{
                name = $field.displayName
                sourceColumn = $field.schemaName.ToLower()
                dataType = "string"
            }
            
            # Add description if available
            if ($field.description) {
                $column.description = $field.description
            }
            
            $columns += $column
        }
        
        # Create the table definition
        $tableDefinition = @{
            name = $displayName
            partitionName = $displayName
            mExpression = $pqContent
            columns = $columns
        }
        
        # Convert to JSON for the API call
        $tableJson = $tableDefinition | ConvertTo-Json -Depth 10 -Compress
        
        Write-Host "  - Schema: $schemaName" -ForegroundColor Gray
        Write-Host "  - Columns: $($columns.Count)" -ForegroundColor Gray
        Write-Host "  - Adding table..." -ForegroundColor Gray
        
        # Output the table definition to a temp file for review
        $tableJson | Out-File "temp_table_$pqFileName.json" -Encoding UTF8
        
        Write-Host "  ✓ Table definition created" -ForegroundColor Green
    }
    else {
        Write-Host "  ✗ Power Query file not found: $pqFile" -ForegroundColor Red
    }
    
    Write-Host ""
}

Write-Host "Table definitions have been created in temp JSON files." -ForegroundColor Green
Write-Host "Review the files starting with 'temp_table_' before importing." -ForegroundColor Yellow
