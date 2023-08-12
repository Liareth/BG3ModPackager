using System.Runtime.InteropServices;

namespace BG3ModPackager;

public static class BinUtils
{
    public static void WriteStruct<T>(BinaryWriter writer, ref T inStruct) where T : struct
    {
        byte[] writeBuffer = StructureToByteArray(inStruct);
        writer.Write(writeBuffer);
    }

    public static void WriteStructs<T>(BinaryWriter writer, T[] elements) where T : struct
    {
        int elementSize = Marshal.SizeOf<T>();
        byte[] writeBuffer = new byte[elementSize * elements.Length];

        for (int i = 0; i < elements.Length; i++)
        {
            byte[] elementBuffer = StructureToByteArray(elements[i]);
            Buffer.BlockCopy(elementBuffer, 0, writeBuffer, i * elementSize, elementSize);
        }

        writer.Write(writeBuffer);
    }

    private static byte[] StructureToByteArray<T>(T structure) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] buffer = new byte[size];

        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        try
        {
            IntPtr pointer = handle.AddrOfPinnedObject();
            Marshal.StructureToPtr(structure, pointer, false);
        }
        finally
        {
            handle.Free();
        }

        return buffer;
    }
}
