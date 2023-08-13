using System.Xml.Serialization;

namespace BG3ModPackager;

[XmlRoot("save")]
public record TextureAtlasLsx
{
    [XmlElement("version")]
    public required TextureAtlasLsxVersion Version { get; init; }

    [XmlElement("region")]
    public List<TextureAtlasLsxRegion> Regions { get; init; } = new();

    public static TextureAtlasLsx Create(TextureAtlasJson atlas)
    {
        int numberOfIcons = atlas.ImageKeys.Count;
        int atlasDimension = NextPowerOfTwo((int)Math.Sqrt(atlas.IconSize * atlas.IconSize * numberOfIcons));
        int iconsPerRow = atlasDimension / atlas.IconSize;

        List<TextureAtlasLsxRegion> regions = new() {
            new() { Id = "TextureAtlasInfo", RootNode =
                new() { Id = "root", Children = new() { Nodes = new() {
                    new() { Id = "TextureAtlasIconSize", Attributes = new() {
                        new() { Id = "Height", Type = "int64", Value = atlas.IconSize.ToString() },
                        new() { Id = "Width", Type = "int64", Value = atlas.IconSize.ToString() }
                    } },
                    new() { Id = "TextureAtlasPath", Attributes = new() {
                        new() { Id = "Path", Type = "LSString", Value = atlas.AtlasDdsPath },
                        new() { Id = "UUID", Type = "FixedString", Value = atlas.AtlasGuid }
                    } },
                    new() { Id = "TextureAtlasTextureSize", Attributes = new() {
                        new() { Id = "Height", Type = "int64", Value = atlasDimension.ToString() },
                        new() { Id = "Width", Type = "int64", Value = atlasDimension.ToString() }
                    } }
                } } }
            }
         };

        float step = atlas.IconSize / (float)atlasDimension;

        List<TextureAtlasLsxNode> uvNodes = new();
        for (int i = 0; i < numberOfIcons; i++)
        {
            int x = i % iconsPerRow;
            int y = i / iconsPerRow;

            float u1 = x * step;
            float u2 = (x + 1) * step;
            float v1 = y * step;
            float v2 = (y + 1) * step;

            uvNodes.Add(new()
            {
                Id = "IconUV",
                Attributes = new() {
                    new() { Id = "MapKey", Type = "FixedString", Value = atlas.ImageKeys[i] },
                    new() { Id = "U1", Type = "float", Value = u1.ToString("F6") },
                    new() { Id = "U2", Type = "float", Value = u2.ToString("F6") },
                    new() { Id = "V1", Type = "float", Value = v1.ToString("F6") },
                    new() { Id = "V2", Type = "float", Value = v2.ToString("F6") }
                }
            });
        }

        regions.Add(new()
        {
            Id = "IconUVList",
            RootNode = new()
            {
                Id = "root",
                Children = new() { Nodes = uvNodes }
            }
        });

        return new()
        {
            Version = new()
            {
                Major = 4,
                Minor = 0,
                Revision = 6,
                Build = 5
            },
            Regions = regions
        };
    }

    private static int NextPowerOfTwo(int x)
    {
        int result = 1;
        while (result < x)
            result <<= 1;
        return result;
    }
}

public record TextureAtlasLsxVersion
{
    [XmlAttribute("major")]
    public int Major { get; init; }

    [XmlAttribute("minor")]
    public int Minor { get; init; }

    [XmlAttribute("revision")]
    public int Revision { get; init; }

    [XmlAttribute("build")]
    public int Build { get; init; }
}

public record TextureAtlasLsxRegion
{
    [XmlAttribute("id")]
    public required string Id { get; init; }

    [XmlElement("node")]
    public required TextureAtlasLsxNode RootNode { get; init; }
}

public record TextureAtlasLsxNode
{
    [XmlAttribute("id")]
    public required string Id { get; init; }

    [XmlElement("children")]
    public TextureAtlasLsxNodesWrapper Children { get; init; }

    [XmlElement("attribute")]
    public List<TextureAtlasLsxAttribute> Attributes { get; init; } = new();
}

public class TextureAtlasLsxNodesWrapper
{
    [XmlElement("node")]
    public List<TextureAtlasLsxNode> Nodes { get; set; } = new();
}

public record TextureAtlasLsxAttribute
{
    [XmlAttribute("id")]
    public required string Id { get; init; }

    [XmlAttribute("type")]
    public required string Type { get; init; }

    [XmlAttribute("value")]
    public required string Value { get; init; }
}