<# 
.SYNOPSIS
    Complete build and deployment script for DataverseToPowerBI XrmToolBox Plugin

.DESCRIPTION
    This script performs a complete clean build, packages the plugin, and deploys to XrmToolBox.
    It handles all dependencies and ensures proper file structure.

.PARAMETER PackageOnly
    Only create the NuGet package without deploying

.PARAMETER DeployOnly  
    Only deploy existing build without rebuilding

.EXAMPLE
    .\Build-And-Deploy.ps1
    Complete build and deploy

.EXAMPLE
    .\Build-And-Deploy.ps1 -PackageOnly
    Build and create NuGet package only
#>

[CmdletBinding()]
param(
    [switch]$PackageOnly,
    [switch]$DeployOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent

# VERSION CONFIGURATION - Read from AssemblyInfo.cs and auto-increment revision (unless DeployOnly)
$assemblyInfoPath = Join-Path $repoRoot "DataverseToPowerBI.XrmToolBox\Properties\AssemblyInfo.cs"
$assemblyContent = Get-Content $assemblyInfoPath -Raw

# Extract current version from AssemblyVersion attribute
if ($assemblyContent -match '\[assembly: AssemblyVersion\("(\d+)\.(\d+)\.(\d+)\.(\d+)"\)\]') {
    $major = [int]$Matches[1]
    $minor = [int]$Matches[2]
    $revision = [int]$Matches[3]
    $build = [int]$Matches[4]
    
    if (-not $DeployOnly) {
        # Auto-increment build for new builds
        $build = $build + 1
        $fullVersion = "$major.$minor.$revision.$build"
        
        # Update AssemblyInfo.cs with new version
        $assemblyContent = $assemblyContent -replace '\[assembly: AssemblyVersion\(".*?"\)\]', "[assembly: AssemblyVersion(`"$fullVersion`")]"
        $assemblyContent = $assemblyContent -replace '\[assembly: AssemblyFileVersion\(".*?"\)\]', "[assembly: AssemblyFileVersion(`"$fullVersion`")]"
        Set-Content -Path $assemblyInfoPath -Value $assemblyContent -NoNewline
        Write-Host "Auto-incremented version: $fullVersion (from AssemblyInfo.cs)" -ForegroundColor Magenta
    } else {
        $fullVersion = "$major.$minor.$revision.$build"
        Write-Host "Current version: $fullVersion (DeployOnly - no increment)" -ForegroundColor Gray
    }
    
} else {
    throw "Could not parse version from AssemblyInfo.cs"
}

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "DataverseToPowerBI XrmToolBox Plugin" -ForegroundColor Cyan
Write-Host "Complete Build & Deploy v$fullVersion" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Function to test if running as admin
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Paths
$xrmToolBoxPluginsPath = "$env:APPDATA\MscrmTools\XrmToolBox\Plugins"
$packageRoot = Join-Path $repoRoot "Package"
$coreProject = Join-Path $repoRoot "DataverseToPowerBI.Core\DataverseToPowerBI.Core.csproj"
$pluginProject = Join-Path $repoRoot "DataverseToPowerBI.XrmToolBox\DataverseToPowerBI.XrmToolBox.csproj"
$nuspecFile = Join-Path $repoRoot "Package\DataverseToPowerBI.XrmToolBox.nuspec"

# Step 1: Clean Build (unless DeployOnly)
if (-not $DeployOnly) {
    Write-Host "[1/6] Cleaning solution..." -ForegroundColor Yellow
    dotnet clean "$repoRoot\DataverseMetadata-to-PowerBI-Semantic-Model.sln" -c Release
    if ($LASTEXITCODE -ne 0) { throw "Clean failed" }
    
    Write-Host "  ✓ Using version $fullVersion" -ForegroundColor Green
    
    Write-Host "[2/6] Building Core library (.NET Framework 4.8)..." -ForegroundColor Yellow
    dotnet build $coreProject -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Core build failed" }
    
    Write-Host "[3/6] Building XrmToolBox plugin (.NET Framework 4.8)..." -ForegroundColor Yellow
    dotnet build $pluginProject -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Plugin build failed" }
    
    Write-Host "✓ Build complete" -ForegroundColor Green
    Write-Host ""
} else {
    Write-Host "Skipping build (DeployOnly mode)" -ForegroundColor Gray
}

# Step 2: Verify build outputs
Write-Host "[3/6] Verifying build outputs..." -ForegroundColor Yellow
$coreDll = Join-Path $repoRoot "DataverseToPowerBI.Core\bin\Release\net48\DataverseToPowerBI.Core.dll"
$pluginDll = Join-Path $repoRoot "DataverseToPowerBI.XrmToolBox\bin\Release\DataverseToPowerBI.XrmToolBox.dll"
$assetsPath = Join-Path $repoRoot "DataverseToPowerBI.XrmToolBox\Assets"

if (-not (Test-Path $coreDll)) { throw "Core DLL not found: $coreDll" }
if (-not (Test-Path $pluginDll)) { throw "Plugin DLL not found: $pluginDll" }
if (-not (Test-Path $assetsPath)) { throw "Assets folder not found: $assetsPath" }

Write-Host "  ✓ Core DLL: $(Split-Path $coreDll -Leaf)" -ForegroundColor Green
Write-Host "  ✓ Plugin DLL: $(Split-Path $pluginDll -Leaf)" -ForegroundColor Green
Write-Host "  ✓ Assets folder found" -ForegroundColor Green
Write-Host ""

# Step 3: Create Package Structure
Write-Host "[4/6] Creating NuGet package structure..." -ForegroundColor Yellow
# Clean only build output subdirectories (preserve script and nuspec in Package/)
foreach ($subDir in @("lib", "content")) {
    $subPath = Join-Path $packageRoot $subDir
    if (Test-Path $subPath) {
        Remove-Item $subPath -Recurse -Force
    }
}

# Create directories
$null = New-Item -ItemType Directory -Path "$packageRoot\lib\net48" -Force
$null = New-Item -ItemType Directory -Path "$packageRoot\content\DataverseToPowerBI" -Force

# Copy assemblies
Copy-Item $pluginDll "$packageRoot\lib\net48\" -Force
Copy-Item $coreDll "$packageRoot\lib\net48\" -Force

# Copy assets (exclude DateTable.tmdl since it's embedded in DLL)
Get-ChildItem "$assetsPath\*" -Recurse | Where-Object { $_.Name -ne "DateTable.tmdl" } | Copy-Item -Destination { 
    $dest = $_.FullName.Replace($assetsPath, "$packageRoot\content\DataverseToPowerBI")
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path $destDir)) { $null = New-Item -ItemType Directory -Path $destDir -Force }
    $dest
} -Force

Write-Host "  ✓ Package structure created" -ForegroundColor Green
Write-Host ""

# Step 4: Create NuGet Package
if (-not $DeployOnly) {
    Write-Host "[5/6] Building NuGet package..." -ForegroundColor Yellow
    
    # Update version in .nuspec file with full version including build number
    $nuspecContent = Get-Content $nuspecFile -Raw
    $nuspecContent = $nuspecContent -replace '<version>.*?</version>', "<version>$fullVersion</version>"
    Set-Content -Path $nuspecFile -Value $nuspecContent -NoNewline
    Write-Host "  ✓ Updated .nuspec version to $fullVersion" -ForegroundColor Green
    
    # Check for nuget.exe
    $nugetExe = Get-Command nuget -ErrorAction SilentlyContinue
    if (-not $nugetExe) {
        Write-Host "  ! NuGet.exe not found in PATH" -ForegroundColor Yellow
        Write-Host "  Downloading nuget.exe..." -ForegroundColor Yellow
        $nugetPath = Join-Path $repoRoot "nuget.exe"
        Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nugetPath
        $nugetExe = $nugetPath
    } else {
        $nugetExe = $nugetExe.Source
    }
    
    & $nugetExe pack $nuspecFile -OutputDirectory $packageRoot -BasePath $repoRoot -Version $fullVersion
    if ($LASTEXITCODE -ne 0) { throw "NuGet pack failed" }
    
    $nupkgFile = Get-ChildItem "$packageRoot\*.nupkg" | Select-Object -First 1
    Write-Host "  ✓ Package created: $($nupkgFile.Name)" -ForegroundColor Green
    Write-Host ""
}

# Step 5: Deploy to XrmToolBox
if (-not $PackageOnly) {
    Write-Host "[6/6] Deploying to XrmToolBox..." -ForegroundColor Yellow
    
    if (-not (Test-Path $xrmToolBoxPluginsPath)) {
        throw "XrmToolBox plugins directory not found: $xrmToolBoxPluginsPath"
    }
    
    # Copy assemblies
    Copy-Item "$packageRoot\lib\net48\*.dll" $xrmToolBoxPluginsPath -Force
    Write-Host "  ✓ Copied DLLs to: $xrmToolBoxPluginsPath" -ForegroundColor Green

    # Note: No additional dependencies needed - XrmToolBox handles authentication
    # and Newtonsoft.Json is already provided by XrmToolBox
    
    # Copy Assets
    $targetAssetsPath = Join-Path $xrmToolBoxPluginsPath "DataverseToPowerBI"
    if (Test-Path $targetAssetsPath) {
        Remove-Item $targetAssetsPath -Recurse -Force
    }
    Copy-Item "$packageRoot\content\DataverseToPowerBI" $xrmToolBoxPluginsPath -Recurse -Force
    Write-Host "  ✓ Copied assets to: $targetAssetsPath" -ForegroundColor Green
    Write-Host ""
    
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "✓ Deployment Complete!" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Files deployed:" -ForegroundColor White
    Write-Host "  - DataverseToPowerBI.XrmToolBox.dll" -ForegroundColor Gray
    Write-Host "  - DataverseToPowerBI.Core.dll" -ForegroundColor Gray
    Write-Host "  - DataverseToPowerBI\ (assets folder)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor White
    Write-Host "  1. Restart XrmToolBox" -ForegroundColor Gray
    Write-Host "  2. Connect to your Dataverse environment" -ForegroundColor Gray
    Write-Host "  3. Launch 'Dataverse to Power BI Semantic Model' plugin" -ForegroundColor Gray
} else {
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host "✓ Package Created!" -ForegroundColor Green
    Write-Host "=====================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Package location: $packageRoot" -ForegroundColor White
}
