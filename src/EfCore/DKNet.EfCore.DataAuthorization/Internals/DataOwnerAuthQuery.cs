using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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
internal sealed class DataOwnerAuthQuery : GlobalQueryFilter
{
    #region Methods

    protected override IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder) =>
        modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(IOwnedBy).IsAssignableFrom(t.ClrType))
            .Where(t => t.GetDiscriminatorValue() == null);

    protected override Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context)
        where TEntity : class
    {
        if (context is not IDataOwnerDbContext dataOwnerContext)
        {
            Debug.Fail("The DbContext must implement IDataOwnerDbContext to use DataOwnerAuthQueryRegister.");
            return null;
        }

        // Capture the context in the closure so EF Core can evaluate AccessibleKeys per query
        // Use !Any() instead of Count == 0 for better SQL translation
        // EF Core can translate Contains on IEnumerable<string> to SQL IN clause
        var capturedContext = dataOwnerContext;

        return (TEntity x) =>
            !capturedContext.AccessibleKeys.Any()
            || capturedContext.AccessibleKeys.Contains(((IOwnedBy)(object)x).OwnedBy);
    }

    #endregion
}