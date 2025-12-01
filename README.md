# UnityZip

A simple tool to extract Unity package (`.unitypackage`) files and get usable files like FBX models, PNG/JPG textures, and icons.

## Quick Start

### For End Users (Easiest Way)

1. **Download the Release**
   - Go to the [Releases page](https://github.com/troyBORG/UnityZip/releases) and choose the version you need:
     - **Windows users (recommended):** Download `UnityZip-Windows-x64.exe` - Standalone executable, no .NET installation required (~36 MB)
     - **Linux users:** Download `UnityZip-Linux-x64` - Standalone executable, no .NET installation required (~37 MB)
     - **Cross-platform (smaller):** Download `UnityZip-Framework-Dependent.zip` - Requires .NET 10.0 Runtime installed (~83 KB)
   - Place the executable in a folder (e.g., `C:\UnityZip\`)

2. **Run the Program**
   - **Option A - Drag and Drop:**
     - Drag your `.unitypackage` file onto `UnityZip.exe`
   
   - **Option B - Command Line:**
     - Open Command Prompt or PowerShell
     - Navigate to where UnityZip.exe is located
     - Run: `UnityZip.exe "path\to\your\file.unitypackage" --raw-only`

3. **Find Your Files**
   - The program creates a folder with the same name as your package file
   - Inside you'll find:
     - `Models/` - All FBX model files (ready to drop into Resonite)
     - `Textures/` - All texture files (PNG, JPG, etc.) (ready to drop into Resonite)
     - `Icons/` - Menu sprites and UI icons (optional for Resonite)
     - `Extracted Unity/` - Complete Unity project structure (for reference - contains everything)

### Examples

```bash
# Extract raw usable files (recommended)
UnityZip.exe "MyPackage.unitypackage" --raw-only

# Extract all files including Unity internal files
UnityZip.exe "MyPackage.unitypackage"

# Extract and overwrite existing files
UnityZip.exe "MyPackage.unitypackage" --raw-only --overwrite
```

## Command Line Options

- `--raw-only` - Extract only usable files (FBX, PNG, JPG, etc.). Skips Unity internal files like .meta, .prefab, .mat
- `--overwrite` - Overwrite existing files if they already exist (default: skip existing files)

## Features

- ✅ Extracts `.unitypackage` files (which are tar.gz archives)
- ✅ **Reconstructs original file paths** from Unity's internal structure
- ✅ **Detects and extracts raw files**: FBX models, PNG/JPG textures, OBJ files, etc.
- ✅ **Smart organization**: Automatically separates models, textures, and icons into separate folders
- ✅ **Excludes GoGo Locomotion icons** from the Icons folder (they go to Textures instead)
- ✅ By default, skips existing files to prevent accidental overwrites

## Output Structure

After extraction, you'll get a folder structure like this:

```
MyPackage/
  Extracted Unity/            # Complete Unity project structure
    Assets/                    # Full Unity Assets folder
      Avatars/
        Viwi/
          Accessories/
            Feathers/
              feathers2.fbx
              Materials/
                Black/
                  t_viwi_Feathers_BaseColor.png
                  ...
      Resources/
        ...
      GoGo/                    # Other Unity folders (if present)
        ...
  
  Models/                     # All FBX model files in one place
    feathers2.fbx
    Pupper.fbx
    ...
  
  Textures/                   # All texture files (excluding icons and GoGo)
    t_viwi_Feathers_BaseColor.png
    t_viwi_Feathers_NormalMap.jpg
    ...
  
  Icons/                      # Menu sprites and UI icons only (GoGo icons excluded)
    viwi_icon_feathers_white.png
    symbol_eye_dilate.png
    ...
```

### For Resonite Users

**Recommended:** Use the organized folders:
- Drag `Models/` folder for the 3D models
- Drag `Textures/` folder for the textures
- Optionally drag `Icons/` if you want the menu sprites

The `Extracted Unity/` folder contains the complete Unity project structure and is provided for reference only.

## For Developers

### Building from Source

```bash
# Clone the repository
git clone <repo-url>
cd UnityZip

# Build
dotnet build

# Run
dotnet run -- "path/to/package.unitypackage" --raw-only

# Build Release
dotnet build -c Release
# Output: bin/Release/net10.0/UnityZip.exe
```

### Release Downloads

When downloading from the [Releases page](https://github.com/troyBORG/UnityZip/releases), you'll find three options:

1. **UnityZip-Windows-x64.exe** (Recommended for Windows)
   - Standalone executable (~36 MB)
   - No .NET installation required
   - Just download and run!

2. **UnityZip-Linux-x64** (For Linux)
   - Standalone executable (~37 MB)
   - No .NET installation required
   - Make executable with: `chmod +x UnityZip-Linux-x64`

3. **UnityZip-Framework-Dependent.zip** (Cross-platform, smaller)
   - Requires .NET 10.0 Runtime installed
   - Works on Windows, Linux, and macOS
   - Much smaller file size (~83 KB)

### Requirements

- **For standalone executables:** None! Just download and run.
- **For framework-dependent version:** .NET 10.0 Runtime must be installed
- **For building from source:** .NET 10.0 SDK required

## About

This project was crafted using [Cursor AI](https://cursor.sh/) to create exactly the tool needed for extracting Unity packages and organizing files for easy use in Resonite and other applications. The AI-assisted development process allowed for rapid iteration and refinement to match specific requirements.
