using System;

namespace Soenneker.QrCodes.Generator.Internal;

internal sealed class QrMatrix
{
    internal bool[,] Modules { get; }
    private readonly bool[,] _reserved;
    private readonly int _size;
    private readonly int _version;
    private readonly QrCodeErrorCorrection _correction;

    internal QrMatrix(int version, QrCodeErrorCorrection correction, byte[] codewords)
    {
        _version = version;
        _correction = correction;
        _size = 17 + 4 * version;
        Modules = new bool[_size, _size];
        _reserved = new bool[_size, _size];
        DrawPatterns();
        PlaceData(codewords);
        int bestMask = 0;
        int bestScore = int.MaxValue;
        for (int mask = 0; mask < 8; mask++)
        {
            ToggleMask(mask);
            DrawFormat(mask);
            int score = Score();
            if (score < bestScore)
            {
                bestScore = score;
                bestMask = mask;
            }
            ToggleMask(mask);
        }
        ToggleMask(bestMask);
        DrawFormat(bestMask);
    }

    private void Set(int x, int y, bool dark)
    {
        Modules[y, x] = dark;
        _reserved[y, x] = true;
    }

    private void DrawPatterns()
    {
        for (int i = 0; i < _size; i++)
        {
            Set(6, i, i % 2 == 0);
            Set(i, 6, i % 2 == 0);
        }
        Finder(3, 3);
        Finder(_size - 4, 3);
        Finder(3, _size - 4);
        if (_version > 1)
        {
            int count = _version / 7 + 2;
            int step = _version == 32 ? 26 : ((_version * 4 + count * 2 + 1) / (count * 2 - 2)) * 2;
            var centers = new int[count];
            centers[0] = 6;
            for (int i = count - 1, coordinate = _size - 7; i > 0; i--, coordinate -= step)
                centers[i] = coordinate;
            for (int row = 0; row < count; row++)
                for (int col = 0; col < count; col++)
                {
                    if ((row == 0 && col == 0) || (row == 0 && col == count - 1) || (row == count - 1 && col == 0))
                        continue;
                    for (int dy = -2; dy <= 2; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                            Set(centers[col] + dx, centers[row] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                }
        }
        DrawFormat(0);
        if (_version >= 7)
        {
            int bits = Bch(_version, 12, 0x1F25);
            for (int bit = 0; bit < 18; bit++)
            {
                bool dark = ((bits >> bit) & 1) != 0;
                Set(_size - 11 + bit % 3, bit / 3, dark);
                Set(bit / 3, _size - 11 + bit % 3, dark);
            }
        }
    }

    private void Finder(int cx, int cy)
    {
        for (int dy = -4; dy <= 4; dy++)
            for (int dx = -4; dx <= 4; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= _size || y >= _size)
                    continue;
                int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                Set(x, y, distance is not (2 or 4));
            }
    }

    private static int Bch(int value, int degree, int polynomial)
    {
        int remainder = value << degree;
        for (int bit = 30; bit >= degree; bit--)
            if ((remainder & (1 << bit)) != 0)
                remainder ^= polynomial << (bit - degree);
        return (value << degree) | remainder;
    }

    private void DrawFormat(int mask)
    {
        int level = _correction switch
        {
            QrCodeErrorCorrection.Low => 1,
            QrCodeErrorCorrection.Medium => 0,
            QrCodeErrorCorrection.Quartile => 3,
            _ => 2
        };
        int bits = Bch((level << 3) | mask, 10, 0x537) ^ 0x5412;
        bool Bit(int i) => ((bits >> i) & 1) != 0;
        for (int i = 0; i < 6; i++) Set(8, i, Bit(i));
        Set(8, 7, Bit(6));
        Set(8, 8, Bit(7));
        Set(7, 8, Bit(8));
        for (int i = 9; i < 15; i++) Set(14 - i, 8, Bit(i));
        for (int i = 0; i < 8; i++) Set(_size - 1 - i, 8, Bit(i));
        for (int i = 8; i < 15; i++) Set(8, _size - 15 + i, Bit(i));
        Set(8, _size - 8, true);
    }

    private void PlaceData(byte[] codewords)
    {
        int bit = 0;
        bool upward = true;
        for (int right = _size - 1; right > 0; right -= 2)
        {
            if (right == 6) right--;
            for (int row = 0; row < _size; row++)
            {
                int y = upward ? _size - 1 - row : row;
                for (int x = right; x >= right - 1; x--)
                {
                    if (_reserved[y, x]) continue;
                    if (bit < codewords.Length * 8)
                        Modules[y, x] = ((codewords[bit / 8] >> (7 - bit % 8)) & 1) != 0;
                    bit++;
                }
            }
            upward = !upward;
        }
        if (bit < codewords.Length * 8 || bit - codewords.Length * 8 > 7)
            throw new InvalidOperationException("QR layout does not match codeword capacity.");
    }

    private void ToggleMask(int mask)
    {
        for (int y = 0; y < _size; y++)
            for (int x = 0; x < _size; x++)
            {
                bool toggle = mask switch
                {
                    0 => (x + y) % 2 == 0,
                    1 => y % 2 == 0,
                    2 => x % 3 == 0,
                    3 => (x + y) % 3 == 0,
                    4 => (y / 2 + x / 3) % 2 == 0,
                    5 => x * y % 2 + x * y % 3 == 0,
                    6 => (x * y % 2 + x * y % 3) % 2 == 0,
                    _ => ((x + y) % 2 + x * y % 3) % 2 == 0
                };
                if (!_reserved[y, x] && toggle) Modules[y, x] = !Modules[y, x];
            }
    }

    private int Score()
    {
        int score = 0, darkCount = 0;
        for (int y = 0; y < _size; y++)
            for (int x = 0; x < _size; x++)
            {
                bool dark = Modules[y, x];
                if (dark) darkCount++;
                if (y > 0 && x > 0 && dark == Modules[y - 1, x] && dark == Modules[y, x - 1] && dark == Modules[y - 1, x - 1])
                    score += 3;
            }
        for (int axis = 0; axis < 2; axis++)
            for (int line = 0; line < _size; line++)
            {
                bool At(int index) => axis == 0 ? Modules[line, index] : Modules[index, line];
                int run = 1;
                for (int i = 1; i < _size; i++)
                {
                    run = At(i) == At(i - 1) ? run + 1 : 1;
                    if (run == 5) score += 3;
                    else if (run > 5) score++;
                }
                // Finder-like 1:1:3:1:1 runs, including scaled patterns and the exterior white border.
                for (int start = 0; start < _size; start++)
                {
                    if (!At(start) || (start > 0 && At(start - 1))) continue;
                    int end = start;
                    while (end < _size && At(end)) end++;
                    int unit = end - start;
                    int finish = start + 7 * unit;
                    if (finish > _size) continue;
                    bool matches = true;
                    for (int i = unit; i < 7 * unit; i++)
                    {
                        bool expected = (i >= 2 * unit && i < 5 * unit) || i >= 6 * unit;
                        if (At(start + i) != expected) { matches = false; break; }
                    }
                    if (!matches || (finish < _size && At(finish))) continue;
                    int before = 0, after = 0;
                    for (int i = start - 1; i >= 0 && !At(i); i--) before++;
                    for (int i = finish; i < _size && !At(i); i++) after++;
                    if (before == start) before += _size;
                    if (finish + after == _size) after += _size;
                    if (before >= 4 * unit && after >= unit) score += 40;
                    if (after >= 4 * unit && before >= unit) score += 40;
                }
            }
        score += Math.Abs(darkCount * 20 - _size * _size * 10) / (_size * _size) * 10;
        return score;
    }
}
