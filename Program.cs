using BG3ModPackager;
using CommandLine;
using System.Diagnostics;
using System.Text.Json;
using System.Xml.Serialization;

const string DivinePath = @"C:\Users\Lia\Desktop\BG3\ExportTool-v1.18.2\Tools\divine.exe";
Parser.Default.ParseArguments<BuildOptions>(args).WithParsed(RunBuildAndPack);

static void RunBuildAndPack(BuildOptions opts)
{
    string sourceDir = opts.SourceDir;
    string targetDir = opts.TargetDir;

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
    foreach (string filePath in files)
    {
        string destFilePath = Path.Combine(targetDir, GetRelativePath(sourceDir, filePath));
        Directory.CreateDirectory(Path.GetDirectoryName(destFilePath)!);

        Log($"Building {filePath}", ConsoleColor.Cyan);

        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string fileExt = Path.GetExtension(destFilePath);

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

                using Image<Rgba32> atlasImage = TextureAtlasBuilder.CreateAtlasFromXml(lsxPath, files);
                string tempFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".png");
                atlasImage.Save(tempFilePath);
                string destPathAtlas = Path.Combine(targetDir, atlasJson.AtlasBasePath, atlasJson.AtlasDdsPath);
                ConvertToDds(tempFilePath, destPathAtlas, DdsType.Bc3);
                File.Delete(tempFilePath);
                Log($"Created atlas {fileName}");

                continue;
            }
            else
            {
                Log($"Unknown asset type {asset.Type}", ConsoleColor.Red);
            }
        }

        if (fileExt == ".lsx")
        {
            File.Copy(filePath, destFilePath);
            string destPath = Path.ChangeExtension(destFilePath, ".lsf");
            Build(filePath, destPath);
            Log($"Compiled {destPath}\n  Including source at {destFilePath}");
            continue;
        }

        if (fileExt == ".xml" && filePath.Contains("Localization"))
        {
            File.Copy(filePath, destFilePath);
            string destPath = Path.ChangeExtension(destFilePath, ".loca");
            LocaResource.ReadFromXml(filePath).SaveTo(destPath);
            Log($"Compiled localization table {fileName} -> {destPath}  Including source at {destFilePath}");
            continue;
        }

        if (fileExt == ".png")
        {
            string destPath = Path.ChangeExtension(destFilePath, ".DDS");
            ConvertToDds(filePath, destPath, DdsType.Bc7);
            Log($"Converted {fileName} -> {destPath}");
            continue;
        }

        if (fileExt == ".xml" || fileExt == ".txt")
        {
            File.Copy(filePath, destFilePath);
            Log($"Included {destFilePath}");
            continue;
        }

        Log($"Warning: Skipped file {fileName}", ConsoleColor.Yellow);
    }

    string packagePath = $"{sourceDir}.pak";
    Log($"Packaging from {targetDir} to {packagePath}");
    Package(targetDir, packagePath);

    if (!string.IsNullOrWhiteSpace(opts.DeployPath))
    {
        Log($"Deploying to {opts.DeployPath}");
        File.Copy(packagePath, opts.DeployPath, overwrite: true);
    }

    Log($"Fin. Remember, Bhaal did nothing wrong!");
}

static string GetRelativePath(string sourceDir, string filePath)
{
    Uri file = new(filePath);
    Uri folder = new(sourceDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
    Uri relativePath = folder.MakeRelativeUri(file);
    return Uri.UnescapeDataString(relativePath.ToString().Replace('/', Path.DirectorySeparatorChar));
}

static void Build(string sourceFile, string destinationFile) =>
    InvokeProcess(DivinePath, $"--game bg3 --action convert-resource --source \"{sourceFile}\" --destination \"{destinationFile}\"");

static void Package(string sourceDir, string targetFile) =>
    InvokeProcess(DivinePath, $"--game bg3 --compression-method none --action create-package --source \"{sourceDir}\" --destination \"{targetFile}\"");

static void ConvertToDds(string inputFilePath, string outputFilePath, DdsType ddsType)
{
    string ddsTypeString = ddsType switch
    {
        DdsType.Bc3 => "BC3_UNORM",
        DdsType.Bc7 => "BC7_UNORM_SRGB",
        _ => throw new NotImplementedException()
    };

    string scratchPath = Path.GetDirectoryName(Path.GetTempPath())!;
    InvokeProcess("texconv.exe", $"-m 1 -f {ddsTypeString} \"{inputFilePath}\" -o \"{scratchPath}\" -y");

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

    [Value(1, MetaName = "target", HelpText = "Target directory.", Required = true)]
    public required string TargetDir { get; set; }

    [Option("deploy", Required = false, HelpText = "Deploy updated package locally.")]
    public string? DeployPath { get; set; } = null;
}

enum DdsType
{
    Bc3,
    Bc7
};
