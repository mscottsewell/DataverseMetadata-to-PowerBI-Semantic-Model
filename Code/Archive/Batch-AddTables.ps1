# Load the table definitions
$tables = (Get-Content "tables_to_add.json" -Raw | ConvertFrom-Json).definitions

Write-Host "Adding $($tables.Count) tables to Power BI model..." -ForegroundColor Cyan
Write-Host ""

$successCount = 0
$failCount = 0

foreach ($table in $tables) {
    $tableName = $table.name
    Write-Host "Adding table: $tableName" -ForegroundColor Yellow
    
    try {
        # Convert single table definition to JSON
        $singleTableDef = @{
            definitions = @($table)
            operation = "Create"
        } | ConvertTo-Json -Depth 15 -Compress
        
        # Save to a temp file for the MCP call
        $tempFile = "temp_single_table.json"
        $singleTableDef | Out-File -FilePath $tempFile -Encoding UTF8
        
        Write-Host "  Table definition saved, ready for import" -ForegroundColor Gray
        Write-Host "  Columns: $($table.columns.Count)" -ForegroundColor Gray
        
        $successCount++
    }
    catch {
        Write-Host "  ✗ Error: $_" -ForegroundColor Red
        $failCount++
    }
    
    Write-Host ""
}

Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Success: $successCount" -ForegroundColor Green
Write-Host "  Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Gray" })

Write-Host ""
Write-Host "Table definitions are ready. Use the MCP tool to import them one at a time." -ForegroundColor Yellow
