using System;

namespace Soenneker.QrCodes.Generator.Internal;

internal static class QrReedSolomon
{
    private static byte Multiply(int left, int right)
    {
        int result = 0;
        while (right != 0)
        {
            if ((right & 1) != 0)
                result ^= left;
            right >>= 1;
            left <<= 1;
            if ((left & 256) != 0)
                left ^= 0x11D; // GF(256) primitive polynomial.
        }
        return (byte)result;
    }

    internal static byte[] CreatePolynomial(int degree)
    {
        byte[] polynomial = [1];
        int root = 1;
        for (int i = 0; i < degree; i++)
        {
            var next = new byte[polynomial.Length + 1];
            for (int j = 0; j < polynomial.Length; j++)
            {
                next[j] ^= polynomial[j];
                next[j + 1] ^= Multiply(polynomial[j], root);
            }
            polynomial = next;
            root = Multiply(root, 2);
        }
        return polynomial;
    }

    internal static byte[] Calculate(byte[] data, byte[] polynomial)
    {
        int degree = polynomial.Length - 1;
        var dividend = new byte[data.Length + degree];
        data.CopyTo(dividend, 0);
        for (int i = 0; i < data.Length; i++)
        {
            byte coefficient = dividend[i];
            for (int j = 0; j < polynomial.Length; j++)
                dividend[i + j] ^= Multiply(polynomial[j], coefficient);
        }
        return dividend.AsSpan(data.Length).ToArray();
    }
}
