using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DKNet.EfCore.DataAuthorization.Internals;

/// <summary>
///     Implements global query filters for entities implementing data ownership.
/// </summary>
/// <remarks>
///     This register automatically applies data authorization filters by:
///     - Detecting entities that implement IOwnedBy
///     - Applying appropriate query filters based on current context
///     - Handling inheritance scenarios correctly
///     - Ensuring proper data visibility based on ownership rules
/// </remarks>
[SuppressMessage("Major Code Smell",
    "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
internal sealed class DataOwnerAuthQueryRegister : IGlobalQueryFilterRegister
{
    private static readonly MethodInfo Method = typeof(DataOwnerAuthQueryRegister)
        .GetMethod(nameof(ApplyQueryFilter), BindingFlags.Static | BindingFlags.NonPublic)!;


    /// <summary>
    ///     Applies the global query filters to the model builder.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <param name="context">The database context.</param>
    public void Apply(ModelBuilder? modelBuilder, DbContext context)
    {
        if (modelBuilder == null)
        {
            Debug.WriteLine($"{nameof(DataOwnerAuthQueryRegister)}-Ignored: ModelBuilder is null.");
            return;
        }

        if (context is not IDataOwnerDbContext keyContext)
        {
            Debug.WriteLine(
                $"{nameof(DataOwnerAuthQueryRegister)}-Ignored: DbContext is not an implementation of IDataKeyContext.");
            return;
        }

        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(IOwnedBy).IsAssignableFrom(t.ClrType))
            .Where(t => t.GetDiscriminatorValue() == null);

        foreach (var entityType in entityTypes)
        {
            var genericMethod = Method.MakeGenericMethod(entityType.ClrType);
            genericMethod.Invoke(null, [modelBuilder, keyContext]);
        }
    }


    /// <summary>
    ///     Applies the ownership query filter to a specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity to filter.</typeparam>
    /// <param name="modelBuilder">The model builder instance.</param>
    /// <param name="context">The data owner context.</param>
    [SuppressMessage("Usage", "MA0002:The IEqualityComparer<string> is not compatible with EfCore.")]
    private static void ApplyQueryFilter<TEntity>(ModelBuilder modelBuilder, IDataOwnerDbContext context)
        where TEntity : class, IOwnedBy
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(x => x.OwnedBy == null || context.AccessibleKeys.Contains(x.OwnedBy));
    }
}