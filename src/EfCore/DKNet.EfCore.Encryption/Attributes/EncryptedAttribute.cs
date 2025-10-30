// <copyright file="EncryptedAttribute.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.EfCore.Encryption.Attributes;

/// <summary>
///     Marks a property for automatic column-level encryption in Entity Framework Core.
/// </summary>
/// <remarks>
///     Apply this attribute to string properties that should be automatically encrypted
///     when persisted to the database and decrypted when retrieved.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute : Attribute;