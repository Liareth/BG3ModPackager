namespace BG3ModPackager;

public class TextureAtlasBuilder
{
    public static Image<Rgba32> CreateAtlasFromXml(TextureAtlasLsx xml, IEnumerable<string> rootFiles)
    {
        TextureAtlasLsxRegion textureAtlasInfo = xml.Regions[0];
        TextureAtlasLsxRegion iconUvList = xml.Regions[1];

        // sorry christ for the whatever the heck this is
        int iconHeight = int.Parse(textureAtlasInfo.RootNode.Children.Nodes[0].Attributes[0].Value);
        int iconWidth = int.Parse(textureAtlasInfo.RootNode.Children.Nodes[0].Attributes[1].Value);
        int atlasHeight = int.Parse(textureAtlasInfo.RootNode.Children.Nodes[2].Attributes[0].Value);
        int atlasWidth = int.Parse(textureAtlasInfo.RootNode.Children.Nodes[2].Attributes[1].Value);

        Image<Rgba32> atlas = new(atlasWidth, atlasHeight);

        foreach (TextureAtlasLsxNode iconUv in iconUvList.RootNode.Children.Nodes)
        {
            string key = iconUv.Attributes[0].Value;
            string? iconPath = rootFiles
                .Select(x => Path.ChangeExtension(x, null))
                .FirstOrDefault(x => x.EndsWith(key));

            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                float u1 = float.Parse(iconUv.Attributes[1].Value);
                float u2 = float.Parse(iconUv.Attributes[2].Value);
                float v1 = float.Parse(iconUv.Attributes[3].Value);
                float v2 = float.Parse(iconUv.Attributes[4].Value);

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
                    icon.Mutate(ctx => ctx.Resize((int)width, (int)height, KnownResamplers.Lanczos3));
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