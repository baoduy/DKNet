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
[SuppressMessage(
    "Major Code Smell",
    "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields")]
internal sealed class DataOwnerAuthQuery : GlobalQueryFilter
{
    #region Properties

    public override string FilterKey => nameof(DataOwnerAuthQuery);

    // Row-level tenant/owner isolation must never be silently disabled by a spec's IsIgnoreQueryFilters flag.
    public override bool IsIgnorable => false;

    #endregion

    #region Methods

    protected override IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder) =>
        modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(IOwnedBy).IsAssignableFrom(t.ClrType))
            .Where(t => t.GetDiscriminatorValue() == null);

    protected override Expression<Func<TEntity, bool>>? HasQueryFilter<TEntity>(DbContext context)
        where TEntity : class
    {
        // Fail closed: a DbContext that cannot supply AccessibleKeys/IsUnrestrictedAccess must never
        // fall through as "no filter", or every IOwnedBy row becomes visible to every caller.
        if (context is not IDataOwnerDbContext dataOwnerContext)
            throw new InvalidOperationException(
                $"DbContext '{context.GetType().Name}' must implement IDataOwnerDbContext to use the data-owner query filter. " +
                "Returning no filter would disable row-level ownership isolation.");

        // Capture the context in the closure so EF Core can evaluate AccessibleKeys/IsUnrestrictedAccess per query
        // An empty AccessibleKeys collection denies access by default (deny-all); only an explicit
        // IsUnrestrictedAccess opt-in bypasses the filter
        // EF Core can translate Contains on IEnumerable<string> to SQL IN clause
        var capturedContext = dataOwnerContext;

        return x =>
            capturedContext.IsUnrestrictedAccess
            || capturedContext.AccessibleKeys.Contains(((IOwnedBy)x).OwnedBy);
    }

    #endregion
}