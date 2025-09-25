﻿using System.Reflection;

namespace DKNet.Svc.PdfGenerator.Services;

/// <summary>
/// Service for loading the content of embedded resources.
/// </summary>
public class EmbeddedResourceService
{
    private readonly Assembly _currentAssembly = Assembly.GetAssembly(typeof(Markdown2PdfConverter))!;

    /// <summary>
    /// Loads the text content of an embedded resource in this <see cref="Assembly"/>.
    /// </summary>
    /// <param name="resourceName">The filename of the resource to load.</param>
    /// <returns>The text content of the resource.</returns>
    internal string GetResourceContent(string resourceName)
    {
        var searchPath = $".{resourceName}";
        var resourcePath = _currentAssembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(searchPath, StringComparison.OrdinalIgnoreCase));

        using var stream = _currentAssembly.GetManifestResourceStream(resourcePath);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}