using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Soenneker.QrCodes.Generator.Internal;

internal static class QrImageWriter
{
    internal static byte[] Png(bool[,] modules, QrCodeOptions options)
    {
        int border = options.DrawQuietZones ? 4 : 0;
        int size = modules.GetLength(0);
        int width = (size + border * 2) * options.PixelsPerModule;
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Span<byte> header = stackalloc byte[13];
        header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], width);
        header[8] = 1; // 1-bit grayscale, no palette or native graphics dependencies.
        WriteChunk(output, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            byte[] row = new byte[1 + (width + 7) / 8];
            for (int y = 0; y < width; y++)
            {
                Array.Fill(row, (byte)255);
                row[0] = 0; // PNG filter None.
                int moduleY = y / options.PixelsPerModule - border;
                if (moduleY >= 0 && moduleY < size)
                    for (int x = 0; x < width; x++)
                    {
                        int moduleX = x / options.PixelsPerModule - border;
                        if (moduleX >= 0 && moduleX < size && modules[moduleY, moduleX])
                            row[1 + x / 8] &= (byte)~(1 << (7 - x % 8));
                    }
                zlib.Write(row);
            }
        }
        WriteChunk(output, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
        return output.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> bytes)
    {
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(value, bytes.Length);
        stream.Write(value);
        stream.Write(type);
        stream.Write(bytes);
        uint crc = uint.MaxValue;
        foreach (byte b in type) crc = UpdateCrc(crc, b);
        foreach (byte b in bytes) crc = UpdateCrc(crc, b);
        BinaryPrimitives.WriteUInt32BigEndian(value, ~crc);
        stream.Write(value);
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (int i = 0; i < 8; i++)
            crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320u);
        return crc;
    }

    internal static string Svg(bool[,] modules, QrCodeOptions options)
    {
        int border = options.DrawQuietZones ? 4 : 0;
        int size = modules.GetLength(0);
        int extent = size + 2 * border;
        int pixels = extent * options.PixelsPerModule;
        var svg = new StringBuilder();
        svg.Append(FormattableString.Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{pixels}\" height=\"{pixels}\" viewBox=\"0 0 {extent} {extent}\" shape-rendering=\"crispEdges\"><rect width=\"100%\" height=\"100%\" fill=\"#fff\"/><path fill=\"#000\" d=\""));
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (!modules[y, x]) continue;
                int start = x;
                while (x + 1 < size && modules[y, x + 1]) x++;
                int length = x - start + 1;
                svg.Append(FormattableString.Invariant($"M{start + border} {y + border}h{length}v1h-{length}z"));
            }
        return svg.Append("\"/></svg>").ToString();
    }
}
