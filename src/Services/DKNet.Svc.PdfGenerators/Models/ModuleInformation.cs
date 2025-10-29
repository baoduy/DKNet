namespace DKNet.Svc.PdfGenerators.Models;

internal class ModuleInformation(string remotePath, string nodePath)
{
    #region Properties

    /// <summary>
    ///     Gets or sets NodePath.
    /// </summary>
    public string NodePath { get; } = nodePath;

    /// <summary>
    ///     Gets or sets RemotePath.
    /// </summary>
    public string RemotePath { get; } = remotePath;

    #endregion

    #region Methods

    public static IReadOnlyDictionary<TKey, ModuleInformation> UpdateDic<TKey>(
        IReadOnlyDictionary<TKey, ModuleInformation> dicToUpdate,
        string path) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(dicToUpdate);
        var updatedLocationMapping = new Dictionary<TKey, ModuleInformation>();

        foreach (var kvp in dicToUpdate)
        {
            var key = kvp.Key;
            var absoluteNodePath = Path.Combine(path, kvp.Value.NodePath);
            updatedLocationMapping[key] = new ModuleInformation(kvp.Value.RemotePath, absoluteNodePath);
        }

        return updatedLocationMapping;
    }

    #endregion
}