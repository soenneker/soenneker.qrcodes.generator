using System;
using System.Buffers.Binary;
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

        byte[] data = new byte[block[0]];
        // The ECI mode (0111), UTF-8 assignment (00011010), and byte mode (0100) occupy two bytes.
        data[0] = 0x71;
        data[1] = 0xA4;
        int payloadStart;
        if (version < 10)
        {
            data[2] = (byte)byteCount;
            payloadStart = 3;
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2), (ushort)byteCount);
            payloadStart = 4;
        }
        Utf8.GetBytes(text, data.AsSpan(payloadStart));
        // A zero byte supplies the terminator and alignment bits when space remains.
        byte pad = 0xEC;
        for (int i = payloadStart + byteCount + 1; i < data.Length; i++)
        {
            data[i] = pad;
            pad ^= 0xFD;
        }

        return new QrMatrix(version, options.ErrorCorrection, Interleave(data, block)).Modules;
    }

    private static byte[] Interleave(byte[] data, int[] layout)
    {
        int count = layout[2] + layout[4];
        var blocks = new ReadOnlyMemory<byte>[count];
        var corrections = new byte[count][];
        byte[] polynomial = QrReedSolomon.CreatePolynomial(layout[1]);
        int offset = 0;
        for (int i = 0; i < count; i++)
        {
            int length = i < layout[2] ? layout[3] : layout[5];
            blocks[i] = data.AsMemory(offset, length);
            corrections[i] = QrReedSolomon.Calculate(blocks[i].Span, polynomial);
            offset += length;
        }
        var result = new byte[data.Length + count * layout[1]];
        int position = 0;
        for (int column = 0; column < Math.Max(layout[3], layout[5]); column++)
            foreach (ReadOnlyMemory<byte> bytes in blocks)
                if (column < bytes.Length)
                    result[position++] = bytes.Span[column];
        for (int column = 0; column < layout[1]; column++)
            foreach (byte[] bytes in corrections)
                result[position++] = bytes[column];
        return result;
    }
}
