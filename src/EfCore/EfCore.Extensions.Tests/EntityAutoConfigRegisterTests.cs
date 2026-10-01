// <copyright file="EntityAutoConfigRegisterTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Extensions.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Extensions.Tests;

public sealed class EntityAutoConfigRegisterTests
{
    #region Methods

    [Fact]
    public void ApplyServices_WithoutModelCustomizer_RegistersAutoConfigOverRelationalCustomizer()
    {
        var services = new ServiceCollection();

        new EntityAutoConfigRegister([typeof(User).Assembly]).ApplyServices(services);

        var original = services.Single(s => s.ServiceType == typeof(ModelCustomizer));
        original.ImplementationType.ShouldBe(typeof(RelationalModelCustomizer));
        original.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        var customizer = services.Single(s => s.ServiceType == typeof(IModelCustomizer));
        customizer.ImplementationType.ShouldBe(typeof(AutoConfigModelCustomizer));
        customizer.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    /// <summary>
    ///     A model built through the auto-config customizer for options that carry no register (an internal service
    ///     provider supplied by the host) applies the live registry, as every auto-configured model does. A plain
    ///     <see cref="DbContext" /> makes the scanned fallback assembly EF Core's own, so the probe builder can reach
    ///     the model only through the registry.
    /// </summary>
    [Fact]
    public void ModelBuiltWithoutRegisterOnOptions_AppliesLiveRegistry()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var services = new ServiceCollection().AddEntityFrameworkSqlite();
        new EntityAutoConfigRegister([typeof(IEntity<>).Assembly]).ApplyServices(services);
        using var internalProvider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder()
            .UseSqlite(connection)
            .UseInternalServiceProvider(internalProvider)
            .Options;
        options.FindExtension<EntityAutoConfigRegister>().ShouldBeNull();

        new ServiceCollection().AddGlobalModelBuilder<RegisterlessProbeBuilder>();
        using var context = new DbContext(options);

        context.Model.FindEntityType(typeof(RegisterlessProbe)).ShouldNotBeNull();
    }

    #endregion
}

internal sealed class RegisterlessProbe
{
    #region Properties

    public int Id { get; set; }

    #endregion
}

/// <summary>
///     Adds <see cref="RegisterlessProbe" /> to a model only when the options carry no register. Once registered it
///     stays in the process-wide registry, so it is inert for every model built through <c>UseAutoConfigModel</c>.
/// </summary>
internal sealed class RegisterlessProbeBuilder : IGlobalModelBuilder
{
    #region Methods

    public void Apply(ModelBuilder modelBuilder, DbContext context)
    {
        if (context.GetService<IDbContextOptions>().FindExtension<EntityAutoConfigRegister>() is not null) return;
        modelBuilder.Entity<RegisterlessProbe>();
    }

    #endregion
}
