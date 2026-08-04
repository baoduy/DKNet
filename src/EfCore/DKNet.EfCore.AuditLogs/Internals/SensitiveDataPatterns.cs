// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SensitiveDataPatterns.cs
// Description: Hardcoded deny-list used to detect audited properties that likely carry sensitive data.

using System.Reflection;

namespace DKNet.EfCore.AuditLogs.Internals;

/// <summary>
///     Detects properties that are likely to carry sensitive data, so their values can be redacted
///     by default in audit logs (see <see cref="AuditPropertyPolicy.RedactSensitive" />).
/// </summary>
internal static class SensitiveDataPatterns
{
    #region Fields

    private static readonly string[] _nameFragments =
    [
        "password",
        "secret",
        "token",
        "apikey",
        "api_key",
        "ssn",
        "socialsecuritynumber",
        "creditcard",
        "cvv",
        "pin",
        "connectionstring",
        "privatekey",
        "passphrase",
        "accesskey",
        "salt"
    ];

    #endregion

    #region Properties

    /// <summary>
    ///     The sentinel value substituted for the real value of a redacted property.
    /// </summary>
    public const string RedactedValue = "***REDACTED***";

    #endregion

    #region Methods

    /// <summary>
    ///     Determines whether the given property is considered sensitive by name or CLR type.
    /// </summary>
    /// <param name="property">The property to inspect.</param>
    /// <returns><c>true</c> if the property's name or type matches a known sensitive pattern.</returns>
    public static bool IsSensitive(PropertyInfo? property)
    {
        if (property is null) return false;

        if (property.PropertyType == typeof(System.Security.SecureString)) return true;

        return _nameFragments.Any(f => property.Name.Contains(f, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
