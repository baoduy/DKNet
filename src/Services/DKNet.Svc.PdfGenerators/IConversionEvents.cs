using System.Diagnostics.CodeAnalysis;

namespace DKNet.Svc.PdfGenerators;

/// <summary>
///     Async event handler delegate for conversion events.
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification =
        "This delegate is specifically designed as an event handler and the suffix accurately describes its purpose")]
public delegate Task AsyncConversionEventHandler<TEventArgs>(object? sender, TEventArgs e) where TEventArgs : EventArgs;

/// <summary>
///     Interface for events that occur during the PDF conversion process.
/// </summary>
public interface IConversionEvents
{
    #region Properties

    /// <summary>
    ///     Name of the output file.
    /// </summary>
    string? OutputFileName { get; }

    #endregion

    /// <summary>
    ///     Raised when markdown is being converted to HTML.
    /// </summary>
    event EventHandler<MarkdownEventArgs>? HtmlConverting;

    /// <summary>
    ///     Raised when the template model is being created. Allows async modification of the template model.
    /// </summary>
    /// <remarks>
    ///     This event uses a custom async event handler pattern to support asynchronous operations.
    ///     The CA1003 warning is suppressed because the async pattern is intentional and required
    ///     for proper async/await support in event handlers.
    /// </remarks>
    [SuppressMessage(
        "Design",
        "CA1003:Use generic event handler instances",
        Justification = "Async event pattern requires custom delegate for proper Task return support")]
    event AsyncConversionEventHandler<TemplateModelEventArgs>? TemplateModelCreating;

    /// <summary>
    ///     Raised after a temporary PDF file is created.
    /// </summary>
    event EventHandler<PdfEventArgs>? TempPdfCreated;
}

/// <summary>
///     <see cref="EventArgs" /> containing the markdown content before the HTML conversion.
/// </summary>
/// <param name="markdownContent">The current markdown content.</param>
public class MarkdownEventArgs(string markdownContent) : EventArgs
{
    #region Properties

    /// <summary>
    ///     The current markdown content, available to be modified.
    /// </summary>
    public string MarkdownContent { get; set; } = markdownContent;

    #endregion
}

/// <summary>
///     <see cref="EventArgs" /> containing the model for the HTML template.
/// </summary>
/// <param name="templateModel">The model for the HMTml template.</param>
public class TemplateModelEventArgs(Dictionary<string, string> templateModel) : EventArgs
{
    #region Properties

    /// <summary>
    ///     The model for the HTML template.
    /// </summary>
    public IDictionary<string, string> TemplateModel { get; } = templateModel;

    #endregion
}

/// <summary>
///     <see cref="EventArgs" /> containing the path to the temporary PDF file.
/// </summary>
/// <param name="pdfPath">Path to the temporary PDF.</param>
public class PdfEventArgs(string pdfPath) : EventArgs
{
    #region Properties

    /// <summary>
    ///     Path to the temporary PDF.
    /// </summary>
    public string PdfPath { get; } = pdfPath;

    #endregion
}