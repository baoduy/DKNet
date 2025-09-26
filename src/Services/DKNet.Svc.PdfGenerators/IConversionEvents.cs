namespace DKNet.Svc.PdfGenerators;

/// <summary>
/// Interface for events that occur during the PDF conversion process.
/// </summary>
public interface IConversionEvents
{
    /// <summary>
    /// Name of the output file.
    /// </summary>
    string? OutputFileName { get; }

    /// <summary>
    /// Raised before markdown is converted to HTML.
    /// </summary>
    event EventHandler<MarkdownEventArgs>? BeforeHtmlConversion;

    /// <summary>
    /// Raised when the template model is being created. Allows async modification of the template model.
    /// </summary>
    event Func<object, TemplateModelEventArgs, Task>? OnTemplateModelCreatingAsync;

    /// <summary>
    /// Raised after a temporary PDF file is created.
    /// </summary>
    event EventHandler<PdfEventArgs>? OnTempPdfCreatedEvent;
}

/// <summary>
/// <see cref="EventArgs"/> containing the markdown content before the HTML conversion.
/// </summary>
/// <param name="markdownContent">The current markdown content.</param>
public class MarkdownEventArgs(string markdownContent) : EventArgs
{
    /// <summary>
    /// The current markdown content, available to be modified.
    /// </summary>
    public string MarkdownContent { get; set; } = markdownContent;
}

/// <summary>
/// <see cref="EventArgs"/> containing the model for the HTML template.
/// </summary>
/// <param name="templateModel">The model for the HMTml template.</param>
public class TemplateModelEventArgs(Dictionary<string, string> templateModel) : EventArgs
{
    /// <summary>
    /// The model for the HTML template.
    /// </summary>
    public IDictionary<string, string> TemplateModel { get; } = templateModel;
}

/// <summary>
/// <see cref="EventArgs"/> containing the path to the temporary PDF file.
/// </summary>
/// <param name="pdfPath">Path to the temporary PDF.</param>
public class PdfEventArgs(string pdfPath) : EventArgs
{
    /// <summary>
    /// Path to the temporary PDF.
    /// </summary>
    public string PdfPath { get; } = pdfPath;
}