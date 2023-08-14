using BG3ModPackager;
using CommandLine;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Serialization;

string _divinePath = GetFullPath("divine.exe");
string _texConvPath = GetFullPath("texconv.exe");

Log($"Divine path: {_divinePath}\nTexconv path: {_texConvPath}");

Parser.Default.ParseArguments<BuildOptions>(args).WithParsed(RunBuildAndPack);

void RunBuildAndPack(BuildOptions opts)
{
    string sourceDir = opts.SourceDir;
    string modName = Path.GetFileName(sourceDir);

    string variantPath = Path.Combine(sourceDir, "Config", "Variants.json");
    BuildVariantJson variants = JsonSerializer.Deserialize<BuildVariantJson>(File.ReadAllText(variantPath))!;

    foreach (BuildVariantJsonEntry entry in variants.Entries)
    {
        string variantName = modName;

        if (!string.IsNullOrWhiteSpace(entry.Name))
        {
            variantName = $"{variantName}_{entry.Name}";
        }

        string variantTargetDir = Path.Combine(Path.GetDirectoryName(sourceDir)!, ".scratch", variantName);
        Log($"{variantName}\nSource: {sourceDir}\nDest: {variantTargetDir}", ConsoleColor.Magenta);
        BuildSingleDirectory(sourceDir, variantTargetDir, GetModifiedFiles(sourceDir, entry));

        string packageName = $"{modName}.pak";
        string packagePath = Path.Combine(variantTargetDir, packageName);

        Log($"Packaging from {variantTargetDir} to {packagePath}");
        Package(variantTargetDir, packagePath);

        if (string.IsNullOrEmpty(entry.Name) && !string.IsNullOrWhiteSpace(opts.DeployDir))
        {
            string deployPath = Path.Combine(opts.DeployDir, packageName);
            Log($"Deploying to {deployPath}");
            File.Copy(packagePath, deployPath, overwrite: true);
        }

        string packagePathZip = Path.Combine(variantTargetDir, $"{variantName}.zip");

        Log($"Zipping {packageName} to {packagePathZip}");
        using ZipArchive archive = new(File.Open(packagePathZip, FileMode.Create), ZipArchiveMode.Create);
        archive.CreateEntryFromFile(packagePath, packageName);
    }
}

void BuildSingleDirectory(
    string sourceDir,
    string targetDir,
    Dictionary<string, string> replacementFiles)
{
    if (!Directory.Exists(sourceDir))
    {
        Log($"Source directory '{sourceDir}' does not exist.");
        return;
    }

    if (Directory.Exists(targetDir))
    {
        Log($"Cleaning up target directory {targetDir}");
        Directory.Delete(targetDir, true);
    }

    Log($"Copying/building all resources to target dir {targetDir}.");

    List<string> files = Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories).ToList();

    foreach (string file in files)
    {
        string filePath = file;
        Log(filePath, ConsoleColor.Cyan);

        string destFilePath = Path.Combine(targetDir, GetRelativePath(sourceDir, filePath));
        Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);

        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string fileExt = Path.GetExtension(destFilePath);

        string filePathRelative = Path.GetRelativePath(sourceDir, filePath).Replace('\\', '/');

        if (replacementFiles.ContainsKey(filePathRelative))
        {
            filePath = Path.Combine(Path.GetTempPath(), filePathRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, replacementFiles[filePathRelative]);
            Log($"  replaced by {filePath}", ConsoleColor.Cyan);
        }

        if (fileExt == ".json")
        {
            string fileContents = File.ReadAllText(filePath);
            AssetJson asset = JsonSerializer.Deserialize<AssetJson>(fileContents)!;

            if (asset.Type == AssetJsonType.TextureAtlas)
            {
                TextureAtlasJson atlasJson = JsonSerializer.Deserialize<TextureAtlasJson>(fileContents)!;
                TextureAtlasLsx atlasLsx = TextureAtlasLsx.Create(atlasJson);

                string lsfDirectory = Path.Combine(targetDir, atlasJson.AtlasBasePath, atlasJson.AtlasLsfPath);
                string lsxPath = Path.Combine(lsfDirectory, $"{fileName}.lsx");
                string lsfPath = Path.ChangeExtension(lsxPath, ".lsf");

                Directory.CreateDirectory(lsfDirectory);

                XmlSerializer serializer = new(typeof(TextureAtlasLsx));
                using (StreamWriter writer = new(lsxPath))
                {
                    serializer.Serialize(writer, atlasLsx);
                }

                Build(lsxPath, lsfPath);
                Log($"Created .lsx/.lsf pair from {fileName}");

                using Image<Rgba32> atlasImage = TextureAtlasBuilder.CreateAtlasFromXml(atlasLsx, files);

                string tempFilePath = Path.GetTempFileName();
                string tempFilePathPng = Path.ChangeExtension(tempFilePath, ".png");
                atlasImage.Save(tempFilePathPng);

                string destPathAtlas = Path.Combine(targetDir, atlasJson.AtlasBasePath, atlasJson.AtlasDdsPath);
                ConvertToDds(tempFilePathPng, destPathAtlas, DdsType.Bc3);

                File.Delete(tempFilePath);
                File.Delete(tempFilePathPng);

                Log($"Created atlas {fileName}");
            }
            else if (asset.Type == AssetJsonType.BuildVariant)
            {
                // Ignored - variant is special case 
            }
            else
            {
                Log($"Unknown asset type {asset.Type}", ConsoleColor.Red);
            }
        }
        else if (fileExt == ".lsx")
        {
            File.Copy(filePath, destFilePath);
            string destPath = Path.ChangeExtension(destFilePath, ".lsf");
            Build(filePath, destPath);
            Log($"Compiled {destPath}\n  Including source at {destFilePath}");
        }
        else if (fileExt == ".png")
        {
            string destPath = Path.ChangeExtension(destFilePath, ".DDS");
            ConvertToDds(filePath, destPath, DdsType.Bc7);
            Log($"Converted {fileName} -> {destPath}");
        }
        else if (fileExt == ".xml")
        {
            File.Copy(filePath, destFilePath);
            Log($"Included {destFilePath}");

            if (filePath.Contains("Localization"))
            {
                string destPath = Path.ChangeExtension(destFilePath, ".loca");
                LocaResource.ReadFromXml(filePath).SaveTo(destPath);
                Log($"Compiled localization table {destPath}");
            }
        }
        else if (fileExt == ".txt")
        {
            File.WriteAllLines(destFilePath, File.ReadAllLines(filePath).Where(line =>
            {
                string trimmedLine = line.TrimStart();
                return !trimmedLine.StartsWith("//"); // strip any lines that start with a comment
            }));

            Log($"Included {destFilePath}");
        }
        else
        {
            Log($"Warning: Skipped file {fileName}", ConsoleColor.Yellow);
        }

        if (file != filePath)
        {
            File.Delete(filePath);
            Log($"Cleaned up temporary file {filePath}");
        }
    }
}

void Build(string sourceFile, string destinationFile) =>
    InvokeProcess(_divinePath, $"--game bg3 --action convert-resource --source \"{sourceFile}\" --destination \"{destinationFile}\"");

void Package(string sourceDir, string destinationFile) =>
    InvokeProcess(_divinePath, $"--game bg3 --compression-method none --action create-package --source \"{sourceDir}\" --destination \"{destinationFile}\"");

void ConvertToDds(string inputFilePath, string outputFilePath, DdsType ddsType)
{
    string ddsTypeString = ddsType switch
    {
        DdsType.Bc3 => "BC3_UNORM",
        DdsType.Bc7 => "BC7_UNORM_SRGB",
        _ => throw new NotImplementedException()
    };

    string scratchPath = Path.GetDirectoryName(Path.GetTempPath())!;
    InvokeProcess(_texConvPath, $"-m 1 -f {ddsTypeString} \"{inputFilePath}\" -o \"{scratchPath}\" -y");

    string inputFileName = Path.GetFileName(inputFilePath);
    string scratchFilePath = Path.Combine(scratchPath, Path.ChangeExtension(inputFileName, ".dds"));

    Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);
    Log($"Moving {scratchFilePath} to {outputFilePath}");
    File.Move(scratchFilePath, outputFilePath);
}

static void InvokeProcess(string fileName, string arguments)
{
    using Process process = Process.Start(new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
    })!;

    Log($"{process.StartInfo.FileName} {process.StartInfo.Arguments}", ConsoleColor.Blue);
    process.WaitForExit();

    ConsoleColor col = process.ExitCode == 0 ? ConsoleColor.Blue : ConsoleColor.Red;
    string processPrefix = $"{Path.GetFileName(fileName)}: ";
    string output = process.StandardOutput.ReadToEnd().Replace("\n", $"\n{processPrefix}");
    Log($"{processPrefix}{output}", col);
}

static void CopyDirectory(string sourceDirName, string destDirName)
{
    Directory.CreateDirectory(destDirName);

    foreach (string file in Directory.GetFiles(sourceDirName))
    {
        string destFile = Path.Combine(destDirName, Path.GetFileName(file));
        File.Copy(file, destFile, true);
    }

    foreach (string dir in Directory.GetDirectories(sourceDirName))
    {
        string destDir = Path.Combine(destDirName, Path.GetFileName(dir));
        CopyDirectory(dir, destDir);
    }
}

static string GetRelativePath(string sourceDir, string filePath)
{
    Uri file = new(filePath);
    Uri folder = new(sourceDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
    Uri relativePath = folder.MakeRelativeUri(file);
    return Uri.UnescapeDataString(relativePath.ToString().Replace('/', Path.DirectorySeparatorChar));
}

static string GetFullPath(string file)
{
    string path = Environment.GetEnvironmentVariable("PATH")!;

    foreach (string dir in path.Split(Path.PathSeparator))
    {
        string filePath = Path.Combine(dir, file);
        if (File.Exists(filePath))
        {
            return filePath;
        }
    }

    return string.Empty;
}

static Dictionary<string, string> GetModifiedFiles(string sourceDir, BuildVariantJsonEntry entry)
{
    Dictionary<string, string> map = new();

    foreach (BuildVariantJsonOperationFindReplace operation in entry.Operations ?? Enumerable.Empty<BuildVariantJsonOperationFindReplace>())
    {
        if (!map.ContainsKey(operation.Path))
        {
            map[operation.Path] = File.ReadAllText(Path.Combine(sourceDir, operation.Path));
        }

        map[operation.Path] = map[operation.Path].Replace(operation.Find, operation.Replace);
    }

    return map;
}

static void Log(string message, ConsoleColor color = ConsoleColor.Gray)
{
    ConsoleColor oldColor = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(message);
    Console.ForegroundColor = oldColor;
}

[Verb("build", HelpText = "Build resources from source to target directory.")]
class BuildOptions
{
    [Value(0, MetaName = "source", HelpText = "Source directory.", Required = true)]
    public required string SourceDir { get; set; }

    [Option("deploy", Required = false, HelpText = "Deploy updated package locally.")]
    public string? DeployDir { get; set; } = null;
}

enum DdsType
{
    Bc3,
    Bc7
};
