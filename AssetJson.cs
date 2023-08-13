namespace BG3ModPackager;

public enum AssetJsonType
{
    TextureAtlas = 0,
    BuildVariant = 1,
}

public record AssetJson(
    AssetJsonType Type);

public record TextureAtlasJson(
    AssetJsonType Type,
    string AtlasBasePath,
    string AtlasLsfPath,
    string AtlasDdsPath,
    string AtlasGuid,
    int IconSize,
    List<string> ImageKeys)
    : AssetJson(Type);

public enum BuildVariantOperationType
{
    FindReplace = 0
}

public record BuildVariantJson(
    AssetJsonType Type,
    List<BuildVariantJsonEntry> Entries);

// TODO: Fix when support more op types
public record BuildVariantJsonEntry(
    string? Name,
    List<BuildVariantJsonOperationFindReplace>? Operations);

public record BuildVariantJsonOperationFindReplace(
    BuildVariantOperationType Type,
    string Path,
    string Find,
    string Replace);
