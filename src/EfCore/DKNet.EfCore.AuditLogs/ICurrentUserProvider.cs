// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ICurrentUserProvider.cs
// Description: Contract for supplying the signed-in user identity that fills audit "created by" / "updated by".

namespace DKNet.EfCore.AuditLogs;

/// <summary>
///     Supplies the identity of the currently signed-in user for audit stamping.
/// </summary>
/// <remarks>
///     Registering an implementation via
///     <see cref="EfCoreAuditLogSetup.AddCurrentUserProvider{TDbContext, TProvider}(Microsoft.Extensions.DependencyInjection.IServiceCollection)" />
///     fills the <c>CreatedBy</c>/<c>UpdatedBy</c> audit properties from <see cref="GetCurrentUser" /> instead of
///     the tenant ownership key. The returned value reaches every registered audit log publisher unmasked — an
///     application under a personal-data rule should return a stable non-personal identifier (for example a
///     subject id) rather than an email address or other personal data.
/// </remarks>
public interface ICurrentUserProvider
{
    #region Methods

    /// <summary>
    ///     Gets the identifier of the currently signed-in user.
    /// </summary>
    /// <returns>The current user's identifier, or <see langword="null" />/empty when no user is available.</returns>
    string? GetCurrentUser();

    #endregion
}
