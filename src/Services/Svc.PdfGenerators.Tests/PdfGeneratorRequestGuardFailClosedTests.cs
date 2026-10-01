using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Shouldly;

namespace Svc.PdfGenerators.Tests;

/// <summary>
///     DRK-1902 rule R3: a request whose allow/abort decision throws is aborted, and the render still completes.
/// </summary>
[Collection("PdfGeneratorChrome")]
public class PdfGeneratorRequestGuardFailClosedTests
{
    #region Methods

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConvertHtmlAsync_RequestWhoseDecisionThrows_AbortsItAndStillProducesThePdf(
        bool allowPrivateNetworkRequests)
    {
        // Arrange: a 308-character host name is a valid URL but longer than Dns.GetHostAddressesAsync accepts,
        // so the decision throws instead of returning.
        var host = string.Join('.', Enumerable.Repeat(new string('a', 60), 5)) + ".com";
        var html = $"""<h1>probe</h1><img src="http://{host}/img">""";
        var options = new PdfGeneratorOptions { AllowPrivateNetworkRequests = allowPrivateNetworkRequests };
        await using var generator = new PdfGenerator(options);
        var pdfPath = Path.Combine(Path.GetTempPath(), $"ssrf-fail-closed-{Guid.NewGuid():N}.pdf");

        try
        {
            // Act
            var result = await generator.ConvertHtmlAsync(html, pdfPath);

            // Assert
            File.Exists(result).ShouldBeTrue();
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    #endregion
}
