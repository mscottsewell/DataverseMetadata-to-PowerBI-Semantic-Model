param(
    [Parameter(Mandatory=$false)]
    [string]$JsonFilePath = "ImaginationWorkshop Metadata Dictionary.json",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputFolder = "PowerQuery"
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
Write-Host "Power Query scripts will be saved to: $OutputFolder" -ForegroundColor Cyan
Write-Host ""

# Read the JSON file
$metadata = Get-Content $JsonFilePath | ConvertFrom-Json

$scriptCount = 0

foreach ($table in $metadata.tables) {
    $displayName = $table.displayName
    $schemaName = $table.schemaName
    
    # Construct the primary key field name (schema name + "id")
    $primaryKeyId = $schemaName.ToLower() + "id"
    
    Write-Host "Generating Power Query for: $displayName ($schemaName)" -ForegroundColor Yellow
    
    # Build the SQL query portion
    $sqlLines = @()
    $sqlLines += "    SELECT Base.$primaryKeyId"
    
    # Add each field
    foreach ($field in $table.fields) {
        $fieldSchema = $field.schemaName.ToLower()
        $sqlLines += "        ,Base.$fieldSchema"
    }
    
    # Add FROM and WHERE clauses
    $sqlLines += "    FROM $schemaName as Base"
    $sqlLines += "    WHERE Base.statecode = 0"
    
    # Join the SQL lines
    $sqlQuery = $sqlLines -join "`r`n"
    
    # Build the complete Power Query M code
    $powerQuery = @"
let
    Dataverse = CommonDataService.Database(DataverseURL,[CreateNavigationProperties=false]),
    Source = Value.NativeQuery(Dataverse,"

$sqlQuery
	
    " ,null ,[EnableFolding=true])
in
    Source
"@
    
    # Create a safe filename from the display name
    $safeFileName = $displayName -replace '[^\w\s-]', '' -replace '\s+', '_'
    $outputFile = Join-Path $OutputFolder "$safeFileName.pq"
    
    # Write the Power Query file
    $powerQuery | Out-File -FilePath $outputFile -Encoding UTF8
    
    Write-Host "  Created: $outputFile" -ForegroundColor Gray
    $scriptCount++
}

Write-Host ""
Write-Host "✓ Successfully generated $scriptCount Power Query scripts!" -ForegroundColor Green
Write-Host ""
Write-Host "Power Query files created in: $OutputFolder" -ForegroundColor Cyan
Write-Host ""
Write-Host "To use in Power BI:" -ForegroundColor Yellow
Write-Host "  1. In Power BI Desktop, go to Home > Get Data > Blank Query" -ForegroundColor White
Write-Host "  2. Open Advanced Editor" -ForegroundColor White
Write-Host "  3. Copy and paste the contents of a .pq file" -ForegroundColor White
Write-Host "  4. Make sure you have a parameter named 'DataverseURL' set to your Dataverse environment URL" -ForegroundColor White
