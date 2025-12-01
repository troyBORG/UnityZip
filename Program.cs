using System.IO.Compression;
using System.Formats.Tar;
using System.Text;

if (args.Length == 0)
{
    Console.WriteLine("Usage: UnityZip <unitypackage_file> [--overwrite] [--raw-only]");
    Console.WriteLine("  --overwrite: Overwrite existing files (default: skip existing files)");
    Console.WriteLine("  --raw-only: Only extract raw usable files (FBX, PNG, JPG, etc.), skip Unity internal files");
    Environment.Exit(1);
}

string unityPackagePath = args[0];
bool overwrite = args.Contains("--overwrite", StringComparer.OrdinalIgnoreCase);
bool rawOnly = args.Contains("--raw-only", StringComparer.OrdinalIgnoreCase);

if (!File.Exists(unityPackagePath))
{
    Console.WriteLine($"Error: File not found: {unityPackagePath}");
    Environment.Exit(1);
}

if (!unityPackagePath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Warning: File doesn't have .unitypackage extension");
}

// Create output directory based on the file name
string fileName = Path.GetFileNameWithoutExtension(unityPackagePath);
string tempExtractDir = Path.Combine(Path.GetDirectoryName(unityPackagePath) ?? ".", fileName + "_temp");
string outputDir = Path.Combine(Path.GetDirectoryName(unityPackagePath) ?? ".", fileName);
string extractedUnityDir = Path.Combine(outputDir, "Extracted Unity");
string assetsDir = Path.Combine(extractedUnityDir, "Assets");

if (!Directory.Exists(tempExtractDir))
{
    Directory.CreateDirectory(tempExtractDir);
}

if (!Directory.Exists(extractedUnityDir))
{
    Directory.CreateDirectory(extractedUnityDir);
}

if (!Directory.Exists(assetsDir))
{
    Directory.CreateDirectory(assetsDir);
}

try
{
    Console.WriteLine($"Step 1: Extracting Unity package structure...");
    
    int filesExtracted = 0;
    
    // First, extract the raw Unity package structure
    using (FileStream fs = new FileStream(unityPackagePath, FileMode.Open, FileAccess.Read))
    using (GZipStream gzip = new GZipStream(fs, CompressionMode.Decompress))
    using (TarReader tar = new TarReader(gzip))
    {
        TarEntry? entry;
        while ((entry = tar.GetNextEntry()) != null)
        {
            if (entry.EntryType == TarEntryType.RegularFile)
            {
                string outputPath = Path.Combine(tempExtractDir, entry.Name);
                string? directory = Path.GetDirectoryName(outputPath);
                
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                using (FileStream outputFile = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    entry.DataStream?.CopyTo(outputFile);
                }
                
                filesExtracted++;
                
                if (filesExtracted % 100 == 0)
                {
                    Console.Write($"\rExtracted {filesExtracted} files from package...");
                }
            }
        }
    }
    
    Console.WriteLine($"\nStep 2: Processing assets and reconstructing file structure...");
    
    // Now process the extracted files to reconstruct the original structure
    if (!Directory.Exists(outputDir))
    {
        Directory.CreateDirectory(outputDir);
    }
    
    if (!Directory.Exists(assetsDir))
    {
        Directory.CreateDirectory(assetsDir);
    }
    
    int rawFilesExtracted = 0;
    int filesSkipped = 0;
    
    // Find all GUID directories
    var guidDirs = Directory.GetDirectories(tempExtractDir)
        .Where(d => Path.GetFileName(d).Length == 32 && 
                   Path.GetFileName(d).All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
        .ToList();
    
    foreach (var guidDir in guidDirs)
    {
        string pathnameFile = Path.Combine(guidDir, "pathname");
        string assetFile = Path.Combine(guidDir, "asset");
        
        if (!File.Exists(pathnameFile) || !File.Exists(assetFile))
            continue;
        
        // Read the original path
        // Unity appends spaces and unreadable characters to pathname files
        // Use LastIndexOfAny to find the last alphabetic character (similar to ResoniteUnityPackagesImporter)
        var rawText = File.ReadAllText(pathnameFile);
        var lastIndex = rawText.LastIndexOfAny("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray());
        if (lastIndex < 0)
            continue;
        string originalPath = rawText.Substring(0, lastIndex + 1);
        if (string.IsNullOrEmpty(originalPath))
            continue;
        
        // Remove "Assets/" prefix if present since we're already extracting to Extracted Unity/Assets/
        if (originalPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            originalPath = originalPath.Substring(7); // Remove "Assets/" (7 characters)
        }
        else if (originalPath.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
        {
            originalPath = originalPath.Substring(7); // Remove "Assets\" (7 characters)
        }
        
        // If path doesn't start with Assets/, it might be a root-level Unity folder (like GoGo, Resources)
        // These should also go into Assets/ folder
        // No change needed - originalPath will be used as-is and placed in assetsDir
        
        // Skip Unity internal files if raw-only mode
        if (rawOnly && (originalPath.EndsWith(".meta") || originalPath.EndsWith(".mat") || 
                       originalPath.EndsWith(".prefab") || originalPath.EndsWith(".unity") ||
                       originalPath.EndsWith(".asset") || originalPath.Contains("/Editor/")))
            continue;
        
        // Read asset data
        byte[] assetData = File.ReadAllBytes(assetFile);
        if (assetData.Length == 0)
            continue;
        
        // Try to detect file type from binary data
        string? detectedExt = FileTypeDetector.DetectExtension(assetData);
        
        // Determine output path - extract directly to Extracted Unity/Assets/
        string outputPath;
        if (detectedExt != null)
        {
            // Use detected extension
            string basePath = Path.ChangeExtension(originalPath, detectedExt);
            outputPath = Path.Combine(assetsDir, basePath);
        }
        else
        {
            // Use original path as-is
            outputPath = Path.Combine(assetsDir, originalPath);
        }
        
        // Create directory if needed
        string? outputDirPath = Path.GetDirectoryName(outputPath);
        if (outputDirPath != null && !Directory.Exists(outputDirPath))
        {
            Directory.CreateDirectory(outputDirPath);
        }
        
        // Check if file exists
        if (File.Exists(outputPath) && !overwrite)
        {
            filesSkipped++;
            continue;
        }
        
        // Try to extract raw binary data
        // For some Unity formats, the actual data might be embedded
        // Try to find PNG/JPEG signatures in the data
        byte[] finalData = assetData;
        
        // Look for embedded image data
        if (detectedExt == null || detectedExt == ".png" || detectedExt == ".jpg")
        {
            // Check if PNG is embedded - look for PNG signature
            int pngStart = -1;
            for (int i = 0; i <= assetData.Length - 8; i++)
            {
                if (assetData[i] == 0x89 && assetData[i + 1] == 0x50 && assetData[i + 2] == 0x4E && assetData[i + 3] == 0x47 &&
                    assetData[i + 4] == 0x0D && assetData[i + 5] == 0x0A && assetData[i + 6] == 0x1A && assetData[i + 7] == 0x0A)
                {
                    pngStart = i;
                    break;
                }
            }
            
            if (pngStart > 0 && pngStart < assetData.Length - 100)
            {
                // Extract PNG from embedded position
                finalData = new byte[assetData.Length - pngStart];
                Array.Copy(assetData, pngStart, finalData, 0, finalData.Length);
                outputPath = Path.ChangeExtension(outputPath, ".png");
            }
            else
            {
                // Check if JPEG is embedded - look for JPEG signature
                int jpegStart = -1;
                for (int i = 0; i <= assetData.Length - 3; i++)
                {
                    if (assetData[i] == 0xFF && assetData[i + 1] == 0xD8 && assetData[i + 2] == 0xFF)
                    {
                        jpegStart = i;
                        break;
                    }
                }
                if (jpegStart > 0 && jpegStart < assetData.Length - 100)
                {
                    // Find JPEG end (0xFF 0xD9)
                    int jpegEnd = assetData.Length;
                    for (int i = jpegStart + 3; i < assetData.Length - 1; i++)
                    {
                        if (assetData[i] == 0xFF && assetData[i + 1] == 0xD9)
                        {
                            jpegEnd = i + 2;
                            break;
                        }
                    }
                    finalData = new byte[jpegEnd - jpegStart];
                    Array.Copy(assetData, jpegStart, finalData, 0, finalData.Length);
                    outputPath = Path.ChangeExtension(outputPath, ".jpg");
                }
            }
        }
        
        // If the asset file starts with a known format, use it directly
        if (detectedExt != null && FileTypeDetector.DetectExtension(assetData) == detectedExt)
        {
            // Data appears to be at the start, use it as-is
            finalData = assetData;
        }
        
        // Write the file
        File.WriteAllBytes(outputPath, finalData);
        rawFilesExtracted++;
        
        if (rawFilesExtracted % 10 == 0)
        {
            Console.Write($"\rProcessed {rawFilesExtracted} raw files...");
        }
    }
    
    // Clean up temp directory
    try
    {
        Directory.Delete(tempExtractDir, true);
    }
    catch { }
    
    Console.WriteLine($"\nStep 3: Organizing files into categories...");
    
    // Create organized folders
    string modelsDir = Path.Combine(outputDir, "Models");
    string texturesDir = Path.Combine(outputDir, "Textures");
    string iconsDir = Path.Combine(outputDir, "Icons");
    
    if (!Directory.Exists(modelsDir)) Directory.CreateDirectory(modelsDir);
    if (!Directory.Exists(texturesDir)) Directory.CreateDirectory(texturesDir);
    if (!Directory.Exists(iconsDir)) Directory.CreateDirectory(iconsDir);
    
    int modelsCopied = 0;
    int texturesCopied = 0;
    int iconsCopied = 0;
    
    // Get all extracted files from Extracted Unity/Assets/
    var allFiles = Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories);
    
    foreach (var file in allFiles)
    {
        string ext = Path.GetExtension(file).ToLowerInvariant();
        string relativePath = Path.GetRelativePath(assetsDir, file);
        string fileBaseName = Path.GetFileName(file);
        
        // Check if this is in an Icons folder, but exclude GoGo Locomotion icons
        string? dirName = Path.GetDirectoryName(relativePath);
        
        // Exclude GoGo Locomotion icons - check if path contains GoGo
        bool isGoGoIcon = relativePath.Contains("GoGo", StringComparison.OrdinalIgnoreCase);
        
        // Only treat as icon if it's in an Icons folder AND not from GoGo
        bool isIcon = !isGoGoIcon && (
                      relativePath.Contains("\\Icons\\", StringComparison.OrdinalIgnoreCase) ||
                      relativePath.Contains("/Icons/", StringComparison.OrdinalIgnoreCase) ||
                      (dirName != null && dirName.EndsWith("Icons", StringComparison.OrdinalIgnoreCase)));
        
        // Copy FBX files to Models folder
        if (ext == ".fbx")
        {
            string destPath = Path.Combine(modelsDir, fileBaseName);
            if (!File.Exists(destPath) || overwrite)
            {
                File.Copy(file, destPath, overwrite);
                modelsCopied++;
            }
        }
        // Copy texture files
        else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".tga" || ext == ".dds")
        {
            // Skip GoGo Locomotion icons completely - don't copy to either Icons or Textures
            if (isGoGoIcon)
            {
                continue; // Skip GoGo icons entirely
            }
            
            if (isIcon)
            {
                // Copy to Icons folder
                string destPath = Path.Combine(iconsDir, fileBaseName);
                // Handle duplicates by including parent folder name
                if (File.Exists(destPath) && !overwrite)
                {
                    string? parentFolder = Path.GetFileName(Path.GetDirectoryName(relativePath));
                    if (parentFolder != null)
                    {
                        destPath = Path.Combine(iconsDir, $"{parentFolder}_{fileBaseName}");
                    }
                }
                File.Copy(file, destPath, overwrite);
                iconsCopied++;
            }
            else
            {
                // Copy to Textures folder
                string destPath = Path.Combine(texturesDir, fileBaseName);
                // Handle duplicates by including parent folder name
                if (File.Exists(destPath) && !overwrite)
                {
                    string? parentFolder = Path.GetFileName(Path.GetDirectoryName(relativePath));
                    if (parentFolder != null)
                    {
                        destPath = Path.Combine(texturesDir, $"{parentFolder}_{fileBaseName}");
                    }
                }
                File.Copy(file, destPath, overwrite);
                texturesCopied++;
            }
        }
    }
    
    Console.WriteLine($"\n\nExtraction complete!");
    Console.WriteLine($"  Raw files extracted: {rawFilesExtracted}");
    if (filesSkipped > 0)
    {
        Console.WriteLine($"  Files skipped (already exist): {filesSkipped}");
    }
    Console.WriteLine($"\nOrganized files:");
    Console.WriteLine($"  Models/ (.fbx): {modelsCopied} files");
    Console.WriteLine($"  Textures/ (.png, .jpg, etc.): {texturesCopied} files");
    Console.WriteLine($"  Icons/ (menu sprites): {iconsCopied} files");
    Console.WriteLine($"\nOutput structure: {outputDir}");
    Console.WriteLine($"  - Models/ (FBX model files - ready for Resonite)");
    Console.WriteLine($"  - Textures/ (texture files - ready for Resonite)");
    Console.WriteLine($"  - Icons/ (menu sprites - optional for Resonite)");
    Console.WriteLine($"  - Extracted Unity/ (complete Unity project structure - for reference)");
}
catch (Exception ex)
{
    Console.WriteLine($"Error extracting package: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

// Helper function to copy directory recursively
static void CopyDirectory(string sourceDir, string destDir, bool overwrite)
{
    Directory.CreateDirectory(destDir);
    
    foreach (var file in Directory.GetFiles(sourceDir))
    {
        string destFile = Path.Combine(destDir, Path.GetFileName(file));
        File.Copy(file, destFile, overwrite);
    }
    
    foreach (var dir in Directory.GetDirectories(sourceDir))
    {
        string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
        CopyDirectory(dir, destSubDir, overwrite);
    }
}

// Helper class to detect file types from binary data
static class FileTypeDetector
{
    public static string? DetectExtension(byte[] data)
    {
        if (data.Length < 4) return null;
        
        // PNG
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return ".png";
        
        // JPEG
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return ".jpg";
        
        // GIF
        if (data.Length >= 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return ".gif";
        
        // TGA
        if (data.Length >= 18)
        {
            // TGA files can start with various patterns, check for common ones
            if ((data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x02) || // Uncompressed RGB
                (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x0A))   // Run-length encoded RGB
                return ".tga";
        }
        
        // FBX - check for "Kaydara" header (ASCII FBX)
        if (data.Length >= 20)
        {
            string header = Encoding.ASCII.GetString(data, 0, Math.Min(20, data.Length));
            if (header.Contains("Kaydara") || header.StartsWith("FBX"))
                return ".fbx";
        }
        
        // OBJ - check for common OBJ keywords
        if (data.Length >= 10)
        {
            string start = Encoding.ASCII.GetString(data, 0, Math.Min(100, data.Length));
            if (start.Contains("v ") || start.Contains("vt ") || start.Contains("vn ") || start.Contains("f "))
                return ".obj";
        }
        
        // Check for Unity YAML format (might contain embedded data)
        if (data.Length >= 10)
        {
            string start = Encoding.UTF8.GetString(data, 0, Math.Min(100, data.Length));
            if (start.StartsWith("%YAML") || start.StartsWith("---"))
            {
                // Try to find embedded binary data
                // Look for common image formats embedded in YAML
                int pngIndex = FindPattern(data, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
                if (pngIndex > 0 && pngIndex < data.Length - 100)
                    return ".png";
            }
        }
        
        return null;
    }
    
    public static int FindPattern(byte[] data, byte[] pattern)
    {
        for (int i = 0; i <= data.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }
}
