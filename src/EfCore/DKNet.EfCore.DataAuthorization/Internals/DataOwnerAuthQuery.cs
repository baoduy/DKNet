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

    /// <remarks>
    ///     The returned predicate ORs <see cref="IDataOwnerDbContext.IsUnrestrictedAccess" /> against a
    ///     <c>Contains</c> check on <see cref="IDataOwnerDbContext.AccessibleKeys" />. That shape is a
    ///     deliberate, reviewed trade-off with two execution consequences, both about the generated SQL
    ///     rather than security:
    ///     <list type="bullet">
    ///         <item>
    ///             the <c>OR &lt;scalar&gt;</c> disjunction is not sargable, so the query optimiser generally
    ///             cannot use an index on <c>OwnedBy</c> — the unrestricted branch means any row might
    ///             qualify regardless of that column;
    ///         </item>
    ///         <item>
    ///             <see cref="IDataOwnerDbContext.AccessibleKeys" /> is an <see cref="IEnumerable{T}" />, so
    ///             EF Core expands the <c>Contains</c> call inline as literal SQL. A caller with 3 accessible
    ///             keys and one with 4 therefore produce different SQL text and different cached query
    ///             plans — plan-cache churn on a multi-tenant system with varying key counts, plus a
    ///             <c>Contains</c>-expansion warning in the EF Core logs.
    ///         </item>
    ///     </list>
    ///     The obvious alternative — drop the <c>OR</c> and keep only the sargable <c>Contains</c>, handling
    ///     unrestricted access by not applying this filter at all (e.g. a separate <see cref="DbContext" />
    ///     configuration or an explicit, audited <c>IgnoreQueryFilters</c>) — was considered and deliberately
    ///     deferred: it moves the unrestricted-access decision from this expression into a different
    ///     mechanism, which is a security-relevant change and needs its own deliberate review rather than
    ///     being folded into an execution-shape fix. <see cref="IsIgnorable" /> stays <see langword="false" />
    ///     precisely so a specification cannot bypass this filter; do not weaken that as a side effect of
    ///     addressing the SQL-shape concern above.
    /// </remarks>
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