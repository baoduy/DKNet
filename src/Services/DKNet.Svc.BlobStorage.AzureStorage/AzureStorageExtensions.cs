// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: AzureStorageExtensions.cs
// Description: Small helper extension methods used by the Azure Storage blob provider.

using System.Diagnostics.CodeAnalysis;

namespace DKNet.Svc.BlobStorage.AzureStorage;

/// <summary>
///     Helper extension methods used by the Azure Storage blob provider.
///     Contains simple path and blob helpers used to normalize paths and determine directory entries.
/// </summary>
[SuppressMessage("Performance", "CA1867:Use char overload")]
public static class AzureStorageExtensions
{
    #region Methods

    /// <summary>
    ///     Ensures the supplied <paramref name="path" /> ends with a forward slash ('/').
    ///     If the path already ends with a slash it is returned unchanged.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The input path guaranteed to end with '/'.</returns>
    public static string EnsureTrailingSlash(this string path) =>
        path.EndsWith("/", StringComparison.OrdinalIgnoreCase) ? path : $"{path}/";

    /// <summary>
    ///     Determines whether the given <paramref name="blob" /> represents a virtual directory entry.
    ///     Azure blob listings can represent directories where ContentLength is zero and ContentType is empty.
    /// </summary>
    /// <param name="blob">The blob item to evaluate.</param>
    /// <returns><c>true</c> when the item appears to be a directory; otherwise <c>false</c>.</returns>
    public static bool IsDirectory(this BlobItem blob) =>
        blob.Properties.ContentLength <= 0 && string.IsNullOrEmpty(blob.Properties.ContentType);

    /// <summary>
    ///     Removes a leading slash from the supplied <paramref name="path" /> if present.
    ///     Useful to convert a path such as "/folder/name" to "folder/name" for client APIs that expect no leading slash.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The input path without a leading slash.</returns>
    public static string RemoveHeadingSlash(this string path) =>
        path.StartsWith("/", StringComparison.OrdinalIgnoreCase) ? path[1..] : path;

    #endregion
}