using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace BG3ModPackager;

// Credit: LSUtils

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LocaHeader
{
    public uint Signature;
    public uint NumEntries;
    public uint TextsOffset;

    public LocaHeader(uint signature, uint numEntries, uint textsOffset)
    {
        Signature = signature;
        NumEntries = numEntries;
        TextsOffset = textsOffset;
    }

    public const uint DefaultSignature = 0x41434f4c; // 'LOCA'
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct LocaEntry
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[] Key;

    public ushort Version;
    public uint Length;

    public LocaEntry(byte[] key, ushort version, uint length)
    {
        Key = key;
        Version = version;
        Length = length;
    }
}

public record LocalizedText(string Key, ushort Version, string Text);

public record LocaResource(List<LocalizedText> Entries)
{
    public static LocaResource ReadFromXml(string path)
    {
        XDocument doc = XDocument.Load(path);

        List<LocalizedText> localizedTexts = new();

        foreach (XElement contentElement in doc.Descendants("content"))
        {
            string key = contentElement.Attribute("contentuid")!.Value;
            ushort version = ushort.Parse(contentElement.Attribute("version")?.Value ?? "0");
            string text = contentElement.Value;

            localizedTexts.Add(new LocalizedText(key, version, text));
        }

        return new LocaResource(localizedTexts);
    }

    public void SaveTo(string path)
    {
        uint offset = (uint)(Marshal.SizeOf(typeof(LocaHeader)) + Marshal.SizeOf(typeof(LocaEntry)) * Entries.Count);
        LocaHeader header = new(LocaHeader.DefaultSignature, (uint)Entries.Count, offset);

        using FileStream stream = new(path, FileMode.CreateNew);
        using BinaryWriter writer = new(stream);
        BinUtils.WriteStruct(writer, ref header);

        LocaEntry[] entries = new LocaEntry[header.NumEntries];

        for (int i = 0; i < entries.Length; ++i)
        {
            LocalizedText entry = Entries[i];
            byte[] keyBytes = Encoding.UTF8.GetBytes(entry.Key);
            byte[] paddedKeyBytes = new byte[64];
            Array.Copy(keyBytes, paddedKeyBytes, Math.Min(keyBytes.Length, 64));
            entries[i] = new(paddedKeyBytes, entry.Version, (uint)Encoding.UTF8.GetByteCount(entry.Text) + 1);
        }

        BinUtils.WriteStructs(writer, entries);

        foreach (LocalizedText entry in Entries)
        {
            byte[] bin = Encoding.UTF8.GetBytes(entry.Text);
            writer.Write(bin);
            writer.Write((byte)0);
        }
    }
}
