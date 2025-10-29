// <copyright file="ColumnEncryptionConverter.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Encryption.Interfaces;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DKNet.EfCore.Encryption.Converters;

/// <summary>
///     EF Core value converter that encrypts string values before storing them in the database
///     and decrypts them when reading from the database.
/// </summary>
/// <param name="encryptionProvider">The encryption provider used to encrypt and decrypt values.</param>
public sealed class ColumnEncryptionConverter(IColumnEncryptionProvider encryptionProvider)
    : ValueConverter<string?, string?>(
        v => encryptionProvider.Encrypt(v),
        v => encryptionProvider.Decrypt(v));