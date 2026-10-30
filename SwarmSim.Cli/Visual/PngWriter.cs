using System.IO.Compression;

namespace SwarmSim.Cli.Visual;

/// <summary>Minimal RGBA8 PNG encoder (stdlib only). Output is byte-stable for equal input on one machine.</summary>
internal static class PngWriter
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    internal static byte[] Encode(int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "PNG dimensions must be positive.");
        if (rgba.Length != checked(width * height * 4))
            throw new ArgumentException("Pixel buffer must hold exactly width * height RGBA pixels.", nameof(rgba));

        using var output = new MemoryStream();
        output.Write(Signature);
        Span<byte> header = stackalloc byte[13];
        WriteBigEndian(header, 0, (uint)width);
        WriteBigEndian(header, 4, (uint)height);
        header[8] = 8;  // bit depth
        header[9] = 6;  // colour type: RGBA
        WriteChunk(output, "IHDR", header);

        using var raw = new MemoryStream();
        using (var zlib = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            int stride = width * 4;
            for (int y = 0; y < height; y++)
            {
                zlib.WriteByte(0); // filter: None
                zlib.Write(rgba.Slice(y * stride, stride));
            }
        }
        WriteChunk(output, "IDAT", raw.GetBuffer().AsSpan(0, (int)raw.Length));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    internal static uint Crc32(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second = default)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in first) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in second) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        WriteBigEndian(word, 0, (uint)data.Length);
        output.Write(word);
        Span<byte> name = stackalloc byte[4];
        for (int i = 0; i < 4; i++) name[i] = (byte)type[i];
        output.Write(name);
        output.Write(data);
        // The CRC covers the chunk type and data, never the length.
        WriteBigEndian(word, 0, Crc32(name, data));
        output.Write(word);
    }

    private static void WriteBigEndian(Span<byte> destination, int offset, uint value)
    {
        destination[offset] = (byte)(value >> 24);
        destination[offset + 1] = (byte)(value >> 16);
        destination[offset + 2] = (byte)(value >> 8);
        destination[offset + 3] = (byte)value;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
