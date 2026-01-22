param(
    [Parameter(Mandatory=$false)]
    [string]$JsonFilePath = "ImaginationWorkshop Metadata Dictionary.json",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputFolder = "SQL"
)

# Validate the JSON file exists
if (-not (Test-Path $JsonFilePath)) {
    Write-Error "JSON file not found: $JsonFilePath"
    exit 1
}

# Create output folder if it doesn't exist
if (-not (Test-Path $OutputFolder)) {
    New-Item -ItemType Directory -Path $OutputFolder | Out-Null
    Write-Host "Created output folder: $OutputFolder" -ForegroundColor Green
}

Write-Host "Reading metadata from: $JsonFilePath" -ForegroundColor Cyan
Write-Host "SQL scripts will be saved to: $OutputFolder" -ForegroundColor Cyan
Write-Host ""

# Read the JSON file
$metadata = Get-Content $JsonFilePath | ConvertFrom-Json

$scriptCount = 0

foreach ($table in $metadata.tables) {
    $displayName = $table.displayName
    $schemaName = $table.schemaName
    
    # Construct the primary key field name (schema name + "id")
    $primaryKeyId = $schemaName.ToLower() + "id"
    
    Write-Host "Generating SQL for: $displayName ($schemaName)" -ForegroundColor Yellow
    
    # Build the SQL script
    $sql = @()
    $sql += "SELECT Base.$primaryKeyId"
    
    # Add each field
    foreach ($field in $table.fields) {
        $fieldSchema = $field.schemaName.ToLower()
        $sql += "     ,Base.$fieldSchema"
    }
    
    # Add FROM and WHERE clauses
    $sql += "FROM $schemaName as Base"
    $sql += "WHERE Base.statecode = 0"
    
    # Create the SQL script content
    $sqlContent = $sql -join "`r`n"
    
    # Create a safe filename from the display name
    $safeFileName = $displayName -replace '[^\w\s-]', '' -replace '\s+', '_'
    $outputFile = Join-Path $OutputFolder "$safeFileName.sql"
    
    # Write the SQL file
    $sqlContent | Out-File -FilePath $outputFile -Encoding UTF8
    
    Write-Host "  Created: $outputFile" -ForegroundColor Gray
    $scriptCount++
}

Write-Host ""
Write-Host "✓ Successfully generated $scriptCount SQL scripts!" -ForegroundColor Green
Write-Host ""
Write-Host "SQL files created in: $OutputFolder" -ForegroundColor Cyan
