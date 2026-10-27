using System;
using Soenneker.QrCodes.Generator.Abstract;
using Soenneker.QrCodes.Generator.Internal;

namespace Soenneker.QrCodes.Generator;

public sealed class QrCodeGenerator : IQrCodeGenerator
{
    public byte[] GeneratePng(string text, QrCodeOptions? options = null)
    {
        options ??= new QrCodeOptions();
        return QrImageWriter.Png(QrEncoder.Encode(text, options), options);
    }

    public string GenerateSvg(string text, QrCodeOptions? options = null)
    {
        options ??= new QrCodeOptions();
        return QrImageWriter.Svg(QrEncoder.Encode(text, options), options);
    }

    public string GeneratePngBase64(string text, QrCodeOptions? options = null) =>
        Convert.ToBase64String(GeneratePng(text, options));

    public string GeneratePngDataUri(string text, QrCodeOptions? options = null) =>
        "data:image/png;base64," + GeneratePngBase64(text, options);
}
