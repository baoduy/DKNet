using System.Diagnostics.CodeAnalysis;

namespace DKNet.Svc.BlobStorage.AzureStorage;

[SuppressMessage("Performance", "CA1867:Use char overload")]
public static class AzureStorageExtensions
{
    #region Methods

    public static string EnsureTrailingSlash(this string path) =>
        path.EndsWith("/", StringComparison.OrdinalIgnoreCase) ? path : $"{path}/";

    public static bool IsDirectory(this BlobItem blob) =>
        blob.Properties.ContentLength <= 0 && string.IsNullOrEmpty(blob.Properties.ContentType);

    public static string RemoveHeadingSlash(this string path) =>
        path.StartsWith("/", StringComparison.OrdinalIgnoreCase) ? path[1..] : path;

    #endregion
}