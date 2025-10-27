namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
///     Margin values with units.
/// </summary>
public class MarginOptions
{
    #region Properties

    /// <inheritdoc cref="PuppeteerSharp.Media.MarginOptions.Bottom" />
    public string? Bottom { get; set; }

    /// <inheritdoc cref="PuppeteerSharp.Media.MarginOptions.Left" />
    public string? Left { get; set; }

    /// <inheritdoc cref="PuppeteerSharp.Media.MarginOptions.Right" />
    public string? Right { get; set; }

    /// <inheritdoc cref="PuppeteerSharp.Media.MarginOptions.Top" />
    public string? Top { get; set; }

    #endregion
}