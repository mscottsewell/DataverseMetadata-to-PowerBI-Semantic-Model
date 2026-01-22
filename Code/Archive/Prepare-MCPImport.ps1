# Add all tables to the Power BI model using MCP commands
# This generates the commands you need to execute

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Power BI Table Addition via MCP Tool" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Read all table definitions from the original JSON
$metadata = Get-Content "tables_to_add.json" -Raw | ConvertFrom-Json

$commands = @()

foreach ($table in $metadata.definitions) {
    $tableName = $table.name
    $columnCount = $table.columns.Count
    
    Write-Host "Table: $tableName ($columnCount columns)" -ForegroundColor Yellow
    
    # Create a simplified command structure
    $cmd = @{
        tableName = $tableName
        columns = $columnCount
        definition = $table
    }
    
    $commands += $cmd
}

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Ready to add $($commands.Count) tables" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "The table definitions are ready in 'tables_to_add.json'" -ForegroundColor White
Write-Host "Use the MCP tool 'mcp_powerbi-model_table_operations' to add them" -ForegroundColor White
