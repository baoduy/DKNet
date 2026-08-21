// <copyright file="SqlServerDependencyListGuardTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Shouldly;

namespace Aspire.Hosting.ServiceBus.Tests;

/// <summary>
///     Guards the invariant that the repository's centrally managed package versions keep the SQL Server
///     entries consumed by <c>DKNet.AspCore.Idempotency.MsSqlStore</c> (plus its Testcontainers-backed test
///     suite) and <c>Aspire.Hosting.ServiceBus</c> (DRK-642 §3): removing SQL Server from tests that don't
///     need it must not strip SQL Server support from the packages and tests that still legitimately depend
///     on it.
/// </summary>
public class SqlServerDependencyListGuardTests
{
    #region Methods

    /// <summary>
    ///     Each SQL Server package entry still consumed elsewhere in the repository must remain declared in
    ///     the shared, centrally managed package version list.
    /// </summary>
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Testcontainers.MsSql")]
    [InlineData("Aspire.Hosting.SqlServer")]
    public void DirectoryPackagesProps_StillDeclaresSqlServerEntry_ConsumedByExistingPackages(string packageId)
    {
        // Arrange
        var propsPath = FindDirectoryPackagesProps();
        var content = File.ReadAllText(propsPath);

        // Act & Assert - fails loudly if a future change strips the entry that
        // DKNet.AspCore.Idempotency.MsSqlStore or Aspire.Hosting.ServiceBus still depends on.
        content.ShouldContain($"PackageVersion Include=\"{packageId}\"");
    }

    private static string FindDirectoryPackagesProps()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DKNet.FW.sln"))) dir = dir.Parent;

        return dir is null
            ? throw new InvalidOperationException("Could not locate DKNet.FW.sln above the test output directory.")
            : Path.Combine(dir.FullName, "Directory.Packages.props");
    }

    #endregion
}
