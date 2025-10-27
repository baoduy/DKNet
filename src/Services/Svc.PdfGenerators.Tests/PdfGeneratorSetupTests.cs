using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Svc.PdfGenerators.Tests;

public class PdfGeneratorSetupTests
{
    [Fact]
    public void AddPdfGenerator_WithoutOptions_RegistersService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPdfGenerator();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var pdfGenerator = serviceProvider.GetService<IPdfGenerator>();
        pdfGenerator.ShouldNotBeNull();
    }

    [Fact]
    public void AddPdfGenerator_WithOptions_RegistersService()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new PdfGeneratorOptions();

        // Act
        services.AddPdfGenerator(options);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var pdfGenerator = serviceProvider.GetService<IPdfGenerator>();
        pdfGenerator.ShouldNotBeNull();
    }

    [Fact]
    public void AddPdfGenerator_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPdfGenerator();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var pdfGenerator1 = serviceProvider.GetService<IPdfGenerator>();
        var pdfGenerator2 = serviceProvider.GetService<IPdfGenerator>();
        
        pdfGenerator1.ShouldBeSameAs(pdfGenerator2);
    }

    [Fact]
    public void AddPdfGenerator_WithNullOptions_RegistersService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPdfGenerator(null);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var pdfGenerator = serviceProvider.GetService<IPdfGenerator>();
        pdfGenerator.ShouldNotBeNull();
    }
}
