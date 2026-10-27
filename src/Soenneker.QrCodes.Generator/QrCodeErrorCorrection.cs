namespace Soenneker.QrCodes.Generator;

/// <summary>The approximate proportion of damaged codewords a QR code can recover.</summary>
public enum QrCodeErrorCorrection
{
    /// <summary>Approximately 7 percent.</summary>
    Low,
    /// <summary>Approximately 15 percent.</summary>
    Medium,
    /// <summary>Approximately 25 percent.</summary>
    Quartile,
    /// <summary>Approximately 30 percent.</summary>
    High
}
