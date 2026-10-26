// <copyright file="EncryptedAttribute.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace DKNet.EfCore.Encryption.Attributes;

/// <summary>
///     Marks a property for automatic column-level encryption in Entity Framework Core.
/// </summary>
/// <remarks>
///     Apply this attribute to string properties of entity or owned types that should be automatically encrypted
///     when persisted to the database and decrypted when retrieved. Members of complex types and members whose type
///     is not <see cref="string" /> are not supported: <c>UseColumnEncryption</c> refuses them at model build.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute : Attribute;