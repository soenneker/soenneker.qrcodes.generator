namespace Soenneker.QrCodes.Generator.Abstract;

/// <summary>
/// Generates black-and-white QR codes using managed, cross-platform renderers.
/// Instances are safe for concurrent use. Text uses UTF-8 byte mode with an ECI marker. QR Model 2 versions 1–40 are supported.
/// </summary>
public interface IQrCodeGenerator
{
    /// <summary>Generates a PNG image containing the supplied text.</summary>
    /// <param name="text">Nonempty text to encode. Whitespace is preserved.</param>
    /// <param name="options">Rendering options, or null for defaults.</param>
    /// <returns>A new PNG byte array owned by the caller.</returns>
    /// <exception cref="System.ArgumentException">Text is null, empty, contains invalid UTF-16, or exceeds QR capacity.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">An option is outside its supported range.</exception>
    byte[] GeneratePng(string text, QrCodeOptions? options = null);

    /// <summary>Generates an SVG document with the same input requirements and exceptions as <see cref="GeneratePng"/>.</summary>
    /// <param name="text">Nonempty text to encode; never inserted as SVG markup.</param>
    /// <param name="options">Rendering options, or null for defaults.</param>
    /// <returns>A standalone SVG string.</returns>
    string GenerateSvg(string text, QrCodeOptions? options = null);

    /// <summary>Generates a Base64-encoded PNG with the same input requirements and exceptions as <see cref="GeneratePng"/>.</summary>
    /// <param name="text">Nonempty text to encode.</param>
    /// <param name="options">Rendering options, or null for defaults.</param>
    /// <returns>Base64 text without a data-URI prefix.</returns>
    string GeneratePngBase64(string text, QrCodeOptions? options = null);

    /// <summary>Generates a PNG data URI with the same input requirements and exceptions as <see cref="GeneratePng"/>.</summary>
    /// <param name="text">Nonempty text to encode.</param>
    /// <param name="options">Rendering options, or null for defaults.</param>
    /// <returns>A data:image/png;base64 URI suitable for an HTML image source.</returns>
    string GeneratePngDataUri(string text, QrCodeOptions? options = null);
}
