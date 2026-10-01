// <copyright file="GlobalModelBuilderModelCacheTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Reflection;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Extensions.Tests;

/// <summary>
///     Regression coverage for DRK-1969: EF Core caches one model per internal service provider, and the process-wide
///     global model builder registry (<c>AddGlobalModelBuilder</c>) was not part of that identity. A context built
///     before a builder was registered left a cached model without it, and every later context with the same options
///     shape reused that model - the data-owner filter failed open.
/// </summary>
/// <remarks>
///     The registry is static and shared by every test in this process, so the probe builder is registered by this
///     test only, lives outside the assembly the probe context scans, and is inert for every model but the probe's.
///     That keeps the Given (model built without the builder) true in any test order.
/// </remarks>
public sealed class GlobalModelBuilderModelCacheTests
{
    #region Fields

    private const string ForeignOwner = "SomeoneElse";
    private const string Owner = "Steven";

    // Holds no IGlobalModelBuilder, so the probe builder reaches the model only through the registry.
    private static readonly Assembly ScannedAssembly = typeof(IEntity<>).Assembly;

    #endregion

    #region Methods

    private static DbContextOptions<OwnerProbeDbContext> BuildOptions(
        SqliteConnection connection,
        bool enableServiceProviderCaching = true) =>
        new DbContextOptionsBuilder<OwnerProbeDbContext>()
            .UseSqlite(connection)
            .EnableServiceProviderCaching(enableServiceProviderCaching)
            .UseAutoConfigModel(ScannedAssembly)
            .Options;

    [Fact]
    public void ContextBuiltBeforeRegistration_DoesNotStripFilterFromLaterContext()
    {
        // Arrange: one shared in-memory database, so every context below reads the same rows.
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Given a context with options shape S builds its model before the builder is registered.
        var optionsBeforeRegistration = BuildOptions(connection);
        var uncachedOptionsBeforeRegistration = BuildOptions(connection, enableServiceProviderCaching: false);
        using (var first = new OwnerProbeDbContext(optionsBeforeRegistration))
        {
            first.Database.EnsureCreated();
            first.Probes.AddRange(new OwnerProbe { Owner = Owner }, new OwnerProbe { Owner = ForeignOwner });
            first.SaveChanges();

            first.Probes.Count(p => p.Owner == Owner).ShouldBe(1);
            first.Probes.Count(p => p.Owner == ForeignOwner).ShouldBe(1);
        }

        // When the builder is registered.
        new ServiceCollection().AddGlobalModelBuilder<OwnerProbeFilter>();

        // Then a second context with the same shape S gets a model with the builder applied.
        using (var second = new OwnerProbeDbContext(BuildOptions(connection)))
        {
            second.Probes.Count(p => p.Owner == Owner).ShouldBe(1);
            second.Probes.Count(p => p.Owner == ForeignOwner).ShouldBe(0);
        }

        // And (DRK-1970) options built before the registration also get the builder: the model is built from the
        // builders registered when the context resolves it, not when its options were built.
        using var uncached = new OwnerProbeDbContext(uncachedOptionsBeforeRegistration);
        uncached.Probes.Count(p => p.Owner == Owner).ShouldBe(1);
        uncached.Probes.Count(p => p.Owner == ForeignOwner).ShouldBe(0);
    }

    #endregion
}

internal sealed class OwnerProbe
{
    #region Properties

    public int Id { get; set; }

    public string Owner { get; set; } = string.Empty;

    #endregion
}

internal sealed class OwnerProbeDbContext(DbContextOptions<OwnerProbeDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<OwnerProbe> Probes => Set<OwnerProbe>();

    #endregion
}

/// <summary>
///     Stands in for <c>DataOwnerAuthQuery</c>: shows only rows owned by "Steven". Once registered it stays in the
///     process-wide registry, so it touches nothing but <see cref="OwnerProbe" />.
/// </summary>
internal sealed class OwnerProbeFilter : IGlobalModelBuilder
{
    #region Methods

    public void Apply(ModelBuilder modelBuilder, DbContext context)
    {
        if (modelBuilder.Model.FindEntityType(typeof(OwnerProbe)) is null) return;
        modelBuilder.Entity<OwnerProbe>().HasQueryFilter(p => p.Owner == "Steven");
    }

    #endregion
}
