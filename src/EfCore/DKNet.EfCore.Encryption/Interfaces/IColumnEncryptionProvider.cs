// <copyright file="IColumnEncryptionProvider.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.EfCore.Encryption.Interfaces;

/// <summary>
///     Defines the contract for column-level encryption and decryption operations.
/// </summary>
public interface IColumnEncryptionProvider
{
    #region Methods

    /// <summary>
    ///     Decrypts the specified ciphertext string.
    /// </summary>
    /// <param name="ciphertext">The ciphertext string to decrypt.</param>
    /// <returns>The decrypted plaintext, or null if the input is null.</returns>
    string? Decrypt(string? ciphertext);

    /// <summary>
    ///     Encrypts the specified plaintext string.
    /// </summary>
    /// <param name="plaintext">The plaintext string to encrypt.</param>
    /// <returns>The encrypted ciphertext, or null if the input is null.</returns>
    string? Encrypt(string? plaintext);

    #endregion
}