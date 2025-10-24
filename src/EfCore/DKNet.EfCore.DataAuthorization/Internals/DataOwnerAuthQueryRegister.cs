using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DKNet.EfCore.Extensions.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
internal sealed class DataOwnerAuthQueryRegister : GlobalQueryFilterRegister
{
    protected override void HasQueryFilter<TEntity>(EntityTypeBuilder<TEntity> builder, DbContext context)
    {
        if (context is not IDataOwnerDbContext dataOwnerContext)
        {
            Debug.Fail("The DbContext must implement IDataOwnerDbContext to use DataOwnerAuthQueryRegister.");
            return;
        }

        builder.HasQueryFilter(x =>
            dataOwnerContext.AccessibleKeys.Count == 0 ||
            dataOwnerContext.AccessibleKeys.Contains(((IOwnedBy)x).OwnedBy));
    }

    protected override IEnumerable<IMutableEntityType> GetEntityTypes(ModelBuilder modelBuilder) =>
        modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(IOwnedBy).IsAssignableFrom(t.ClrType))
            .Where(t => t.GetDiscriminatorValue() == null);
}