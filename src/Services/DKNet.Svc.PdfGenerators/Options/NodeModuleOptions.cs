namespace DKNet.Svc.PdfGenerators.Options;

/// <summary>
///     Load modules from a local <i>node_module</i> directory.
/// </summary>
/// <param name="ModulePath">Path to the node_module directory.</param>
public record NodeModuleOptions(string ModulePath) : ModuleOptions(ModuleLocation.Custom);