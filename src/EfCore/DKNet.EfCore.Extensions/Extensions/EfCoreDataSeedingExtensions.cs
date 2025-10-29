// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: EfCoreDataSeedingExtensions.cs
// Description: Helpers to register and run IDataSeedingConfiguration implementations found in assemblies.

using DKNet.EfCore.Extensions.Configurations;
using DKNet.Fw.Extensions.TypeExtractors;

namespace DKNet.EfCore.Extensions.Extensions;

/// <summary>
///     Extension helpers to auto-register and run data seeding configurations discovered in assemblies.
/// </summary>
public static class EfCoreDataSeedingExtensions
{
    #region Methods

    /// <summary>
    ///     Discover types that implement <see cref="IDataSeedingConfiguration" /> from the provided assemblies.
    /// </summary>
    /// <param name="assemblies">A collection of assemblies to scan. May be null.</param>
    /// <returns>An array of types implementing IDataSeedingConfiguration. Returns an empty array when none found.</returns>
    private static Type[] GetDataSeedingTypes(this ICollection<Assembly>? assemblies)
    {
        if (assemblies == null || assemblies.Count == 0) return [];

        var types = assemblies.Extract()
            .Classes()
            .IsInstanceOf<IDataSeedingConfiguration>();

        // Materialize to array to avoid deferred execution and potential multiple enumeration problems
        return [.. types];
    }

    /// <summary>
    ///     Registers model-managed seed data from discovered <see cref="IDataSeedingConfiguration" /> types.
    ///     This will call HasData on the model for any configuration that exposes non-empty HasData collections.
    /// </summary>
    /// <param name="modelBuilder">The model builder to register seed data on.</param>
    /// <param name="assemblies">Assemblies to scan for IDataSeedingConfiguration implementations.</param>
    internal static void RegisterDataSeeding(this ModelBuilder modelBuilder, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var seedingTypes = assemblies.GetDataSeedingTypes();
        foreach (var seedingType in seedingTypes)
        {
            if (Activator.CreateInstance(seedingType) is not IDataSeedingConfiguration seedingInstance) continue;

            var data = seedingInstance.HasData?.ToList() ?? [];
            if (data.Count == 0) continue;

            var entityType = seedingInstance.EntityType;
            // ModelBuilder.Entity(Type).HasData accepts params object[]
            modelBuilder.Entity(entityType).HasData(data.ToArray());
        }
    }

    /// <summary>
    ///     Configure the <see cref="DbContextOptionsBuilder" /> to automatically run data seeding callbacks
    ///     discovered in the provided assemblies during migrations or startup.
    /// </summary>
    /// <param name="this">The options builder to configure.</param>
    /// <param name="assemblies">Assemblies to scan for IDataSeedingConfiguration implementations.</param>
    /// <returns>The options builder for chaining.</returns>
    public static DbContextOptionsBuilder UseAutoDataSeeding(this DbContextOptionsBuilder @this, Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(@this);
        ArgumentNullException.ThrowIfNull(assemblies);

        // Discover seeding types and create instances once
        var seedingTypes = assemblies.GetDataSeedingTypes();
        var seedingInstances = seedingTypes
            .Select(s => Activator.CreateInstance(s) as IDataSeedingConfiguration)
            .OfType<IDataSeedingConfiguration>()
            .ToList();

        // Synchronous seeding hook
        @this.UseSeeding((context, performedStoreOperation) =>
        {
            foreach (var s in seedingInstances)
                s.SeedAsync?.Invoke(context, performedStoreOperation, CancellationToken.None)
                    .GetAwaiter().GetResult();
        });

        // Asynchronous seeding hook
        @this.UseAsyncSeeding(async (context, performedStoreOperation, cancellation) =>
        {
            foreach (var s in seedingInstances)
                if (s.SeedAsync != null)
                    await s.SeedAsync.Invoke(context, performedStoreOperation, cancellation).ConfigureAwait(false);
        });

        return @this;
    }

    #endregion
}