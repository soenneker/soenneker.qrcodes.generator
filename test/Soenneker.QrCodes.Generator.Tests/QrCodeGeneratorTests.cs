using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Soenneker.QrCodes.Generator.Abstract;
using Soenneker.QrCodes.Generator.Registrars;
using StbImageSharp;
using ZXing;

namespace Soenneker.QrCodes.Generator.Tests;

public sealed class QrCodeGeneratorTests
{
    private readonly IQrCodeGenerator _generator = new QrCodeGenerator();

    [Test]
    public void Every_version_and_correction_level_decodes_at_capacity()
    {
        ZXing.QrCode.Internal.ErrorCorrectionLevel[] levels =
        [ZXing.QrCode.Internal.ErrorCorrectionLevel.L, ZXing.QrCode.Internal.ErrorCorrectionLevel.M,
            ZXing.QrCode.Internal.ErrorCorrectionLevel.Q, ZXing.QrCode.Internal.ErrorCorrectionLevel.H];
        for (int version = 1; version <= 40; version++)
            for (int level = 0; level < 4; level++)
            {
                var specification = ZXing.QrCode.Internal.Version.getVersionForNumber(version);
                int capacity = specification.TotalCodewords - specification.getECBlocksForLevel(levels[level]).TotalECCodewords;
                int length = capacity - (version < 10 ? 3 : 4);
                var random = new Random(version * 4 + level);
                string text = new string(Enumerable.Range(0, length).Select(_ => (char)random.Next(33, 127)).ToArray());
                var options = new QrCodeOptions { Version = version, ErrorCorrection = (QrCodeErrorCorrection)level, PixelsPerModule = 2 };
                ImageResult image = ImageResult.FromMemory(_generator.GeneratePng(text, options), ColorComponents.RedGreenBlue);
                var reader = new BarcodeReaderGeneric { Options = new ZXing.Common.DecodingOptions { PureBarcode = true, PossibleFormats = [BarcodeFormat.QR_CODE] } };
                Result result = reader.Decode(image.Data, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGB24);
                if (result?.Text != text || image.Width != (17 + 4 * version + 8) * 2)
                    throw new Exception($"Version {version}, level {level} failed at capacity {length}; decoded length {result?.Text?.Length}, width {image.Width}, expected {(17 + 4 * version + 8) * 2}.");
                Expect<ArgumentException>(() => _generator.GeneratePng(text + "x", options));
                ImageResult automatic = ImageResult.FromMemory(_generator.GeneratePng(text, options with { Version = 0 }));
                if (automatic.Width != image.Width)
                    throw new Exception($"Automatic version selection failed for {version}/{level}.");
            }
    }

    [Test]
    public void Svg_geometry_matches_png_and_is_culture_independent()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            var options = new QrCodeOptions { PixelsPerModule = 1 };
            XElement svg = XDocument.Parse(_generator.GenerateSvg("SVG 世界", options)).Root!;
            ImageResult png = ImageResult.FromMemory(_generator.GeneratePng("SVG 世界", options), ColorComponents.Grey);
            var pixels = Enumerable.Repeat((byte)255, png.Width * png.Height).ToArray();
            string path = svg.Element(XName.Get("path", svg.Name.NamespaceName))!.Attribute("d")!.Value;
            foreach (Match match in Regex.Matches(path, @"M(\d+) (\d+)h(\d+)v1h-\d+z"))
            {
                int x = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int y = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                int width = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                Array.Fill(pixels, (byte)0, y * png.Width + x, width);
            }
            if (!pixels.SequenceEqual(png.Data))
                throw new Exception("SVG geometry differs from PNG.");
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Test]
    [Arguments("https://soenneker.com/?a=1&b=2")]
    [Arguments("Hello 世界 🌍 café")]
    [Arguments("  whitespace\n\t")]
    [Arguments("<script>alert('hello')</script>")]
    public void Png_round_trips_text(string text)
    {
        foreach (QrCodeErrorCorrection level in Enum.GetValues<QrCodeErrorCorrection>())
        {
            byte[] png = _generator.GeneratePng(text, new QrCodeOptions { ErrorCorrection = level });
            ImageResult image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlue);
            var reader = new BarcodeReaderGeneric { Options = new ZXing.Common.DecodingOptions { PossibleFormats = [BarcodeFormat.QR_CODE] } };
            Result result = reader.Decode(image.Data, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGB24);
            if (result?.Text != text)
                throw new Exception($"Round trip failed at {level}: {result?.Text}");
        }
    }

    [Test]
    public void Scale_and_quiet_zone_control_dimensions()
    {
        ImageResult normal = ImageResult.FromMemory(_generator.GeneratePng("hello"));
        ImageResult cropped = ImageResult.FromMemory(_generator.GeneratePng("hello", new QrCodeOptions { DrawQuietZones = false }));
        ImageResult doubled = ImageResult.FromMemory(_generator.GeneratePng("hello", new QrCodeOptions { PixelsPerModule = 16 }));
        if (normal.Width != normal.Height || normal.Width - cropped.Width != 64 || doubled.Width != normal.Width * 2)
            throw new Exception("Incorrect image dimensions or quiet zone.");
    }

    [Test]
    public void Svg_is_valid_and_does_not_embed_payload_markup()
    {
        const string text = "<script>alert('hello')</script>";
        XElement svg = XDocument.Parse(_generator.GenerateSvg(text)).Root!;
        if (svg.Name != XName.Get("svg", "http://www.w3.org/2000/svg") || !svg.Descendants().Any() || svg.ToString().Contains("script"))
            throw new Exception("Invalid or unsafe SVG.");
    }

    [Test]
    public void Base64_and_data_uri_contain_the_png()
    {
        byte[] expected = _generator.GeneratePng("hello");
        string base64 = _generator.GeneratePngBase64("hello");
        if (!expected.SequenceEqual(Convert.FromBase64String(base64)) || _generator.GeneratePngDataUri("hello") != "data:image/png;base64," + base64)
            throw new Exception("Encoded PNG differs.");
    }

    [Test]
    public void Rejects_invalid_inputs()
    {
        Expect<ArgumentNullException>(() => _generator.GeneratePng(null!));
        Expect<ArgumentException>(() => _generator.GenerateSvg(""));
        foreach (int scale in new[] { 0, -1, 65, int.MaxValue })
            Expect<ArgumentOutOfRangeException>(() => _generator.GeneratePng("hello", new QrCodeOptions { PixelsPerModule = scale }));
        Expect<ArgumentOutOfRangeException>(() => _generator.GenerateSvg("hello", new QrCodeOptions { ErrorCorrection = (QrCodeErrorCorrection)99 }));
        Expect<ArgumentOutOfRangeException>(() => _generator.GeneratePng("hello", new QrCodeOptions { Version = 41 }));
        Expect<ArgumentOutOfRangeException>(() => _generator.GenerateSvg("hello", new QrCodeOptions { Version = -1 }));
        Expect<System.Text.EncoderFallbackException>(() => _generator.GeneratePng("\uD800"));
        Expect<ArgumentException>(() => _generator.GeneratePng(new string('x', 10000)));
    }

    [Test]
    public void Singleton_supports_concurrent_generation()
    {
        var services = new ServiceCollection();
        services.AddQrCodeGeneratorAsSingleton();
        using ServiceProvider provider = services.BuildServiceProvider();
        IQrCodeGenerator generator = provider.GetRequiredService<IQrCodeGenerator>();
        byte[] expected = generator.GeneratePng("concurrent");
        Parallel.For(0, 16, _ =>
        {
            if (!expected.SequenceEqual(generator.GeneratePng("concurrent")))
                throw new Exception("Concurrent generation differs.");
        });
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
}
