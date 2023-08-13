namespace BG3ModPackager;

public enum AssetJsonType
{
    TextureAtlas = 0,
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
