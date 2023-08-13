using System.Xml.Linq;

namespace BG3ModPackager;

public class TextureAtlasBuilder
{
    public static Image<Rgba32> CreateAtlasFromXml(string xmlPath, IEnumerable<string> rootFiles)
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

                ApplyAlphaAdjustment(icon);

                atlas.Mutate(ctx => ctx.DrawImage(icon, new Point((int)x, (int)y), 1.0f));
            }
        }

        return atlas;
    }

    // * Stretch alpha non-linearly such that the almost invisible parts become much more visible
    // * Normalize such that our lowest alpha should be 255.
    private static void ApplyAlphaAdjustment(Image<Rgba32> icon)
    {
        byte maxAlpha = 0;

        for (int i = 0; i < icon.Width; i++)
        {
            for (int j = 0; j < icon.Height; j++)
            {
                Rgba32 pixel = icon[i, j];
                byte denorm = (byte)Math.Clamp(Math.Pow(pixel.A / 255.0, 0.5) * 255, 0, 255);
                maxAlpha = Math.Max(maxAlpha, denorm);
                icon[i, j] = pixel with { A = denorm };
            }
        }

        double alphaScale = maxAlpha / 255.0 + 0.5;

        for (int i = 0; i < icon.Width; i++)
        {
            for (int j = 0; j < icon.Height; j++)
            {
                Rgba32 pixel = icon[i, j];
                byte denorm = (byte)Math.Clamp(pixel.A * alphaScale, 0, 255);
                icon[i, j] = pixel with { A = denorm };
            }
        }
    }
}