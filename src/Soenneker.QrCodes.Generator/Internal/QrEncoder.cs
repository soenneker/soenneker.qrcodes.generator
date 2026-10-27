using System;
using System.Collections.Generic;
using System.Text;

namespace Soenneker.QrCodes.Generator.Internal;

internal static class QrEncoder
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static bool[,] Encode(string text, QrCodeOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        if (options.PixelsPerModule is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(options.PixelsPerModule));
        if (!Enum.IsDefined(options.ErrorCorrection))
            throw new ArgumentOutOfRangeException(nameof(options.ErrorCorrection));
        if (options.Version is < 0 or > 40)
            throw new ArgumentOutOfRangeException(nameof(options.Version));

        // Count before allocating so oversized inputs do not allocate a second large buffer.
        int byteCount = Utf8.GetByteCount(text);
        int version = options.Version == 0 ? 1 : options.Version;
        int[] block;
        while (true)
        {
            block = QrBlockTable.Rows[(version - 1) * 4 + (int)options.ErrorCorrection];
            int countBits = version < 10 ? 8 : 16;
            if (byteCount < (1 << countBits) && 16L + countBits + byteCount * 8L <= block[0] * 8)
                break;
            if (options.Version != 0 || version == 40)
                throw new ArgumentException("Text exceeds QR capacity for the selected version and error correction level.", nameof(text));
            version++;
        }

        byte[] payload = Utf8.GetBytes(text);
        byte[] data = new byte[block[0]];
        int position = 0;
        void Write(int value, int length)
        {
            for (int bit = length - 1; bit >= 0; bit--, position++)
                data[position / 8] |= (byte)(((value >> bit) & 1) << (7 - position % 8));
        }
        Write(7, 4); // ECI segment, assignment 26 (UTF-8).
        Write(26, 8);
        Write(4, 4); // Byte mode.
        Write(payload.Length, version < 10 ? 8 : 16);
        foreach (byte value in payload)
            Write(value, 8);
        Write(0, Math.Min(4, data.Length * 8 - position));
        Write(0, (8 - position % 8) % 8);
        int pad = 0xEC;
        while (position < data.Length * 8)
        {
            Write(pad, 8);
            pad ^= 0xFD;
        }

        return new QrMatrix(version, options.ErrorCorrection, Interleave(data, block)).Modules;
    }

    private static byte[] Interleave(byte[] data, int[] layout)
    {
        int count = layout[2] + layout[4];
        var blocks = new byte[count][];
        var corrections = new byte[count][];
        byte[] polynomial = QrReedSolomon.CreatePolynomial(layout[1]);
        int offset = 0;
        for (int i = 0; i < count; i++)
        {
            int length = i < layout[2] ? layout[3] : layout[5];
            blocks[i] = data.AsSpan(offset, length).ToArray();
            corrections[i] = QrReedSolomon.Calculate(blocks[i], polynomial);
            offset += length;
        }
        var result = new List<byte>(data.Length + count * layout[1]);
        for (int column = 0; column < Math.Max(layout[3], layout[5]); column++)
            foreach (byte[] bytes in blocks)
                if (column < bytes.Length)
                    result.Add(bytes[column]);
        for (int column = 0; column < layout[1]; column++)
            foreach (byte[] bytes in corrections)
                result.Add(bytes[column]);
        return result.ToArray();
    }
}
