using System.Xml.Linq;

namespace BG3ModPackager;

public record TextureAtlas(Image<Rgba32> Image, string DesiredRelativePath) : IDisposable
{
    public void Dispose()
    {
        Image.Dispose();
    }
}

public class TextureAtlasBuilder
{
    public static TextureAtlas CreateAtlasFromXml(string xmlPath, IEnumerable<string> rootFiles)
    {
        XDocument document = XDocument.Parse(File.ReadAllText(xmlPath));

        // sorry christ for the whatever the heck this is
        int iconHeight = int.Parse(document.Descendants("node").First(n => n.Attribute("id")!.Value == "TextureAtlasIconSize").Element("attribute")!.Attribute("value")!.Value);
        int iconWidth = int.Parse(document.Descendants("node").First(n => n.Attribute("id")!.Value == "TextureAtlasIconSize").Elements("attribute").First(a => a.Attribute("id")!.Value == "Width").Attribute("value")!.Value);
        int atlasHeight = int.Parse(document.Descendants("node").First(n => n.Attribute("id")!.Value == "TextureAtlasTextureSize").Element("attribute")!.Attribute("value")!.Value);
        int atlasWidth = int.Parse(document.Descendants("node").First(n => n.Attribute("id")!.Value == "TextureAtlasTextureSize").Elements("attribute").First(a => a.Attribute("id")!.Value == "Width").Attribute("value")!.Value);

        Image<Rgba32> atlas = new(atlasWidth, atlasHeight);

        foreach (XElement iconNode in document.Descendants("node").Where(n => n.Attribute("id")!.Value == "IconUV"))
        {
            string key = iconNode.Elements("attribute").First(a => a.Attribute("id")!.Value == "MapKey").Attribute("value")!.Value;
            string? iconPath = rootFiles
                .Select(x => Path.ChangeExtension(x, null))
                .FirstOrDefault(x => x.EndsWith(key));

            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                float u1 = float.Parse(iconNode.Elements("attribute").First(a => a.Attribute("id")!.Value == "U1").Attribute("value")!.Value);
                float u2 = float.Parse(iconNode.Elements("attribute").First(a => a.Attribute("id")!.Value == "U2").Attribute("value")!.Value);
                float v1 = float.Parse(iconNode.Elements("attribute").First(a => a.Attribute("id")!.Value == "V1").Attribute("value")!.Value);
                float v2 = float.Parse(iconNode.Elements("attribute").First(a => a.Attribute("id")!.Value == "V2").Attribute("value")!.Value);

                float x = u1 * atlasWidth;
                float y = v1 * atlasHeight;
                float width = u2 * atlasWidth - x;
                float height = v2 * atlasHeight - y;

                if (iconWidth != width || iconHeight != height)
                {
                    throw new Exception($"Expected icon {iconWidth}x{iconHeight} got {width}x{height}");
                }

                using Image<Rgba32> icon = Image.Load<Rgba32>(rootFiles.First(x => x.StartsWith(iconPath)));

                if (icon.Width != width || icon.Height != height)
                {
                    icon.Mutate(ctx => ctx.Resize((int)width, (int)height));
                }

                for (int i = 0; i < icon.Width; i++)
                {
                    for (int j = 0; j < icon.Height; j++)
                    {
                        Rgba32 pixel = icon[i, j];
                        pixel.A = (byte)Math.Clamp(pixel.A * 1.5, 0, 255);
                        icon[i, j] = pixel;
                    }
                }

                atlas.Mutate(ctx => ctx.DrawImage(icon, new Point((int)x, (int)y), 1f));
            }

        }

        string relativePath = document.Descendants("node").First(n => n.Attribute("id")!.Value == "TextureAtlasPath").Elements("attribute").First(a => a.Attribute("id")!.Value == "Path").Attribute("value")!.Value;
        return new(atlas, relativePath);
    }
}