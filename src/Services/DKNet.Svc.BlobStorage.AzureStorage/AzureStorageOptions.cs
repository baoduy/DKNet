// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: AzureStorageOptions.cs
// Description: Configuration options for the Azure Blob Storage provider.

namespace DKNet.Svc.BlobStorage.AzureStorage;

/// <summary>
///     Options for configuring the Azure Blob Storage provider. Inherits common blob service settings from
///     <see cref="BlobServiceOptions" /> and adds Azure-specific connection settings.
/// </summary>
public class AzureStorageOptions : BlobServiceOptions
{
    #region Properties

    /// <summary>
    ///     The factory method used to create the <see cref="BlobServiceClient" /> instance.
    /// </summary>
    public Func<AzureStorageOptions, Task<BlobServiceClient>>? BlobServiceClientFactory { get; set; }

    /// <summary>
    ///     The Azure Storage connection string used to create a <c>BlobServiceClient</c>.
    ///     This value is required for the Azure provider to initialize correctly.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    ///     The container name to use for blob operations.
    ///     The container will be created if it does not exist when the provider initializes.
    /// </summary>
    public string ContainerName { get; set; } = null!;

    /// <summary>
    ///     The configuration section key used when binding Azure storage options from configuration.
    ///     Typical configuration key: "BlobService:AzureStorage".
    /// </summary>
    public static string Name => "BlobService:AzureStorage";

    #endregion
}