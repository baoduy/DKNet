using System.Reflection;

namespace DKNet.Svc.PdfGenerators.Services;

/// <summary>
///     Service for loading the content of embedded resources.
/// </summary>
public class EmbeddedResourceService
{
    #region Fields

    private readonly Assembly _currentAssembly = Assembly.GetAssembly(typeof(PdfGenerator))!;

    #endregion

    #region Methods

    /// <summary>
    ///     Loads the text content of an embedded resource in this <see cref="Assembly" /> asynchronously.
    /// </summary>
    /// <param name="resourceName">The filename of the resource to load.</param>
    /// <returns>The text content of the resource.</returns>
    internal async Task<string> GetResourceContentAsync(string resourceName)
    {
        var searchPath = $".{resourceName}";
        var resourcePath = this._currentAssembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(searchPath, StringComparison.OrdinalIgnoreCase));

        await using var stream = this._currentAssembly.GetManifestResourceStream(resourcePath);
        using var reader = new StreamReader(stream!);
        return await reader.ReadToEndAsync();
    }

    #endregion
}