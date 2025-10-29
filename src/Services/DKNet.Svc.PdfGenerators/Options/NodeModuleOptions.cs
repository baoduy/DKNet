namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
///     Load modules from a local <i>node_module</i> directory.
/// </summary>
/// <param name="ModulePath">Path to the node_module directory.</param>
/// <summary>
///     NodeModuleOptions operation.
/// </summary>
/// <returns>The result of the operation.</returns>
public record NodeModuleOptions(string ModulePath) : ModuleOptions(ModuleLocation.Custom);