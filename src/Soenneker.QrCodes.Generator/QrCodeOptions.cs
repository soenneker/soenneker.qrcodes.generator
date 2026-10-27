namespace Soenneker.QrCodes.Generator;

/// <summary>Immutable rendering options shared by PNG and SVG output.</summary>
public sealed record QrCodeOptions
{
    /// <summary>QR version from 1 through 40, or 0 to select the smallest version that fits. Defaults to 0.</summary>
    public int Version { get; init; }

    /// <summary>Pixels per module, from 1 through 64. Defaults to 8.</summary>
    public int PixelsPerModule { get; init; } = 8;

    /// <summary>Error correction strength. Defaults to Quartile.</summary>
    public QrCodeErrorCorrection ErrorCorrection { get; init; } = QrCodeErrorCorrection.Quartile;

    /// <summary>Includes the four-module white border required by scanners. Defaults to true.
    /// Disable only when the caller supplies an equivalent border.</summary>
    public bool DrawQuietZones { get; init; } = true;
}
