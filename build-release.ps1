# UnityZip Release Build Script
# This script builds all release variants and creates a GitHub release

Write-Host "=== UnityZip Release Build Script ===" -ForegroundColor Cyan
Write-Host ""

# Configuration
$version = "v1.0.0"
$releaseName = "UnityZip $version - Initial Release"
$releaseNotes = @'
# UnityZip v1.0.0 - Initial Release

A powerful tool to extract Unity package (`.unitypackage`) files and organize them for easy use.

## Features

✅ **Extract Unity Packages** - Extract `.unitypackage` files (which are tar.gz archives)
✅ **Smart File Organization** - Automatically organizes files into:
   - `Models/` - All FBX model files
   - `Textures/` - All texture files (PNG, JPG, etc.)
   - `Icons/` - Menu sprites and UI icons
   - `Extracted Unity/` - Complete Unity project structure for reference

✅ **Intelligent Detection** - Automatically detects and extracts raw files (FBX, PNG, JPG, OBJ, etc.)
✅ **GoGo Locomotion Filter** - Automatically excludes GoGo Locomotion icons from organized folders
✅ **Ready for Resonite** - Organized folders are ready to drop directly into Resonite

## Downloads

**Standalone Executables (No .NET Required):**
- `UnityZip-Windows-x64.exe` - Windows standalone executable (~36 MB)
- `UnityZip-Linux-x64` - Linux standalone executable (~37 MB)

**Framework-Dependent (Requires .NET 10.0 Runtime):**
- `UnityZip-Framework-Dependent.zip` - Cross-platform version (~83 KB, requires .NET 10.0)

## Usage

```bash
# Extract raw usable files (recommended)
UnityZip.exe "MyPackage.unitypackage" --raw-only

# Extract all files including Unity internal files
UnityZip.exe "MyPackage.unitypackage"

# Extract and overwrite existing files
UnityZip.exe "MyPackage.unitypackage" --raw-only --overwrite
```

## For Resonite Users

Simply drag the organized folders into Resonite:
- `Models/` folder for 3D models
- `Textures/` folder for textures
- `Icons/` folder (optional) for menu sprites

The `Extracted Unity/` folder contains the complete Unity project structure and is provided for reference only.
'@

# Clean previous builds
Write-Host "Step 1: Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Clean failed!" -ForegroundColor Red
    exit 1
}

# Build framework-dependent version
Write-Host "`nStep 2: Building framework-dependent version..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Create framework-dependent zip
Write-Host "`nStep 3: Creating framework-dependent zip..." -ForegroundColor Yellow
$frameworkDir = "bin\Release\net10.0"
$frameworkZip = "UnityZip-Framework-Dependent.zip"
if (Test-Path $frameworkZip) { Remove-Item $frameworkZip }
Compress-Archive -Path "$frameworkDir\UnityZip.exe", "$frameworkDir\UnityZip.dll", "$frameworkDir\UnityZip.runtimeconfig.json", "$frameworkDir\UnityZip.deps.json" -DestinationPath $frameworkZip
Write-Host "Created: $frameworkZip" -ForegroundColor Green

# Build Windows self-contained
Write-Host "`nStep 4: Building Windows self-contained executable..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) {
    Write-Host "Windows build failed!" -ForegroundColor Red
    exit 1
}

# Rename Windows executable
$winExe = "bin\Release\net10.0\win-x64\publish\UnityZip.exe"
$winExeRenamed = "bin\Release\net10.0\win-x64\publish\UnityZip-Windows-x64.exe"
if (Test-Path $winExeRenamed) { Remove-Item $winExeRenamed }
Rename-Item -Path $winExe -NewName "UnityZip-Windows-x64.exe"
Write-Host "Created: UnityZip-Windows-x64.exe" -ForegroundColor Green

# Build Linux self-contained
Write-Host "`nStep 5: Building Linux self-contained executable..." -ForegroundColor Yellow
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) {
    Write-Host "Linux build failed!" -ForegroundColor Red
    exit 1
}

# Rename Linux executable
$linuxExe = "bin\Release\net10.0\linux-x64\publish\UnityZip"
$linuxExeRenamed = "bin\Release\net10.0\linux-x64\publish\UnityZip-Linux-x64"
if (Test-Path $linuxExeRenamed) { Remove-Item $linuxExeRenamed }
Rename-Item -Path $linuxExe -NewName "UnityZip-Linux-x64"
Write-Host "Created: UnityZip-Linux-x64" -ForegroundColor Green

# Create GitHub release
Write-Host "`nStep 6: Creating GitHub release..." -ForegroundColor Yellow

$assets = @(
    $frameworkZip,
    "bin\Release\net10.0\win-x64\publish\UnityZip-Windows-x64.exe",
    "bin\Release\net10.0\linux-x64\publish\UnityZip-Linux-x64"
)

# Check if release already exists
$existingRelease = gh release view $version 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Release $version already exists. Deleting..." -ForegroundColor Yellow
    gh release delete $version --yes
}

# Create new release
Write-Host "Creating release $version..." -ForegroundColor Yellow
gh release create $version --title $releaseName --notes $releaseNotes $assets

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n=== Release Created Successfully! ===" -ForegroundColor Green
    Write-Host "Release: https://github.com/troyBORG/UnityZip/releases/tag/$version" -ForegroundColor Cyan
} else {
    Write-Host "`nRelease creation failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nBuild complete!" -ForegroundColor Green

