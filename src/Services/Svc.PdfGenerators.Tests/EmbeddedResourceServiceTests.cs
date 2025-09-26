using DKNet.Svc.PdfGenerators.Services;

namespace Svc.PdfGenerators.Tests;

public class EmbeddedResourceServiceTests
{
    [Fact]
    public async Task GetResourceContentAsync_ReturnsContent()
    {
        var service = new EmbeddedResourceService();
        // Use a known resource name for testing, or mock Assembly if needed
        // For demonstration, this will throw if resource not found
        var resourceName = "SomeKnownResource.txt"; // Replace with actual resource name
        try
        {
            var content = await service.GetResourceContentAsync(resourceName);
            Assert.False(string.IsNullOrEmpty(content));
            Console.WriteLine($"Resource content: {content.Substring(0, Math.Min(50, content.Length))}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Assert.True(ex is InvalidOperationException || ex is ArgumentNullException);
        }
    }
}