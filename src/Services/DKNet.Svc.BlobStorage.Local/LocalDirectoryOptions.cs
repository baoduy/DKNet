// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: LocalDirectoryOptions.cs
// Description: Configuration options for the local filesystem blob storage provider.

using DKNet.Svc.BlobStorage.Abstractions;

namespace DKNet.Svc.BlobStorage.Local;

/// <summary>
///     Options specific to the local filesystem blob storage provider. Inherits common
///     blob configuration from <see cref="BlobServiceOptions" /> and adds a root folder setting.
/// </summary>
public class LocalDirectoryOptions : BlobServiceOptions
{
    #region Properties

    /// <summary>
    ///     The configuration section name used to bind <see cref="LocalDirectoryOptions" /> from configuration.
    ///     Typical key: "BlobStorage:LocalFolder".
    /// </summary>
    public static string Name => "BlobStorage:LocalFolder";

    /// <summary>
    ///     The root folder on the local filesystem where blobs will be stored. When not set, the consuming
    ///     service may fall back to a default folder (for example a working-directory relative path).
    /// </summary>
    public string? RootFolder { get; set; }

    #endregion
}