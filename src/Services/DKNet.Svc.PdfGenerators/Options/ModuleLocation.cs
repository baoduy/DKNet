namespace DKNet.Svc.PdfGenerators.Options;

/// <inheritdoc cref="ModuleOptions.ModuleLocation" />
/// <summary>
///     Defines ModuleLocation values.
/// </summary>
public enum ModuleLocation
{
    /// <inheritdoc cref="ModuleOptions.None" />
    None = 0,

    /// <inheritdoc cref="ModuleOptions.Remote" />
    Remote,

    /// <inheritdoc cref="ModuleOptions.FromLocalPath(string)" />
    Custom
}