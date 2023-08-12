using BG3ModPackager;
using CommandLine;
using System.Diagnostics;

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
        string relativePath = GetRelativePath(sourceDir, filePath);
        string destFilePath = Path.Combine(targetDir, relativePath);
        string destDir = Path.GetDirectoryName(destFilePath)!;
        Directory.CreateDirectory(destDir);

        Log($"Building {filePath}", ConsoleColor.Cyan);

        string name = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(destFilePath);

        if (extension == ".lsx")
        {
            File.Copy(filePath, destFilePath);
            string destPath = Path.ChangeExtension(destFilePath, ".lsf");
            Build(filePath, destPath);
            Log($"Compiled {destPath}\n  Including source at {destFilePath}");

            if (name.Contains("Atlas"))
            {
                using TextureAtlas atlas = TextureAtlasBuilder.CreateAtlasFromXml(filePath, files);
                string tempFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".png");
                atlas.Image.Save(tempFilePath);

                string destPathAtlas = Path.Combine(targetDir, "Public", "DyeDyeDye", atlas.DesiredRelativePath);
                ConvertToDdsBc3(tempFilePath, destPathAtlas);
                File.Delete(tempFilePath);

                Log($"Generated atlas {destPathAtlas}");
            }

            continue;
        }

        if (extension == ".xml" && filePath.Contains("Localization"))
        {
            File.Copy(filePath, destFilePath);
            string destPath = Path.ChangeExtension(destFilePath, ".loca");
            LocaResource.ReadFromXml(filePath).SaveTo(destPath);
            Log($"Compiled localization table {name} -> {destPath}  Including source at {destFilePath}");
            continue;
        }

        if (extension == ".png")
        {
            string destPath = Path.ChangeExtension(destFilePath, ".DDS");
            ConvertToDdsBc3(filePath, destPath);
            Log($"Converted {name} -> {destPath}");
            continue;
        }

        if (extension == ".xml" || extension == ".txt")
        {
            File.Copy(filePath, destFilePath);
            Log($"Included {destFilePath}");
            continue;
        }

        Log($"Warning: Skipped file {name}", ConsoleColor.Yellow);
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

static void ConvertToDdsBc3(string inputFilePath, string outputFilePath)
{
    string scratchPath = Path.GetDirectoryName(Path.GetTempPath())!;
    InvokeProcess("texconv.exe", $"-m 1 -f BC3_UNORM_SRGB \"{inputFilePath}\" -o \"{scratchPath}\" -y");

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
