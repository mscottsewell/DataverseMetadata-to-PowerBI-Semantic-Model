# Script to add all 12 tables to the Power BI model
# This script creates individual JSON files for each table that can be used with the MCP tool

$metadata = Get-Content "tables_to_add.json" -Raw | ConvertFrom-Json

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Power BI Table Import Helper" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Create a folder for individual table JSONs
$outputFolder = "TableImports"
if (-not (Test-Path $outputFolder)) {
    New-Item -ItemType Directory -Path $outputFolder | Out-Null
}

$tableCount = 0

foreach ($table in $metadata.definitions) {
    $tableCount++
    $tableName = $table.name
    $safeFileName = $tableName -replace '[^\w\s-]', '' -replace '\s+', '_'
    
    # Create individual table definition
    $singleTableDef = @{
        definitions = @($table)
        operation = "Create"
    }
    
    # Save to individual JSON file
    $outputFile = Join-Path $outputFolder "$safeFileName.json"
    $singleTableDef | ConvertTo-Json -Depth 15 | Out-File -FilePath $outputFile -Encoding UTF8
    
    Write-Host "[$tableCount/12] $tableName" -ForegroundColor Yellow
    Write-Host "   File: $outputFile" -ForegroundColor Gray
    Write-Host "   Columns: $($table.columns.Count)" -ForegroundColor Gray
    Write-Host ""
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "✓ All table definitions created!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Files saved in: $outputFolder" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Review the table definitions in the $outputFolder folder" -ForegroundColor White
Write-Host "2. Use the Power BI MCP tool to import each table" -ForegroundColor White
Write-Host "3. The tables will be created with proper column names and descriptions" -ForegroundColor White
