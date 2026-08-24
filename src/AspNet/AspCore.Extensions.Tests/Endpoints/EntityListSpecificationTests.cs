using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Covers the ordering rules of <c>EntityListSpecification</c> directly — specifically that a caller-chosen
///     <c>orderBy</c> always carries a unique tie-break. This has to be asserted at the specification level:
///     the HTTP fixtures run on EF InMemory, whose LINQ-to-objects sort is stable, so a paging walk over tied
///     values passes there even when the real database would repeat and drop rows across page boundaries.
/// </summary>
public class EntityListSpecificationTests
{
    #region Methods

    [Fact]
    public void CallerOrderBy_AppendsIdDescendingAsTieBreak()
    {
        // Name is not unique in general; without a tie-break, rows with equal names have no defined order and
        // paging over them is non-deterministic on a real database.
        var spec = new EntityListSpecification<WidgetEntity, Guid, WidgetModel>(
            new ListQuery<WidgetEntity>(Filter: null, OrderBy: "Name", Descending: false));

        spec.OrderByQueries.Count.ShouldBe(1);
        spec.OrderByDescendingQueries.Count.ShouldBe(1);

        // The appended clause must select Id — anything else would not be unique.
        var widget = new WidgetEntity(new Guid("00000000-0000-0000-0000-000000000042"), "w");
        var tieBreak = spec.OrderByDescendingQueries.Single().Compile();
        tieBreak(widget).ShouldBe(widget.Id);
    }

    [Fact]
    public void CallerOrderByDescending_AppendsIdTieBreak()
    {
        var spec = new EntityListSpecification<WidgetEntity, Guid, WidgetModel>(
            new ListQuery<WidgetEntity>(Filter: null, OrderBy: "Name", Descending: true));

        spec.OrderByQueries.Count.ShouldBe(0);
        spec.OrderByDescendingQueries.Count.ShouldBe(2);
    }

    [Fact]
    public void CallerOrderById_DoesNotAppendASecondIdClause()
    {
        // Id is already unique — a second Id key would be dead weight in every ORDER BY.
        var spec = new EntityListSpecification<WidgetEntity, Guid, WidgetModel>(
            new ListQuery<WidgetEntity>(Filter: null, OrderBy: "Id", Descending: false));

        (spec.OrderByQueries.Count + spec.OrderByDescendingQueries.Count).ShouldBe(1);
    }

    [Fact]
    public void NoCallerOrder_KeepsTheNewestFirstDefault()
    {
        var spec = new EntityListSpecification<WidgetEntity, Guid, WidgetModel>(
            new ListQuery<WidgetEntity>(Filter: null, OrderBy: null, Descending: false));

        // Non-audited entity: Id descending alone.
        spec.OrderByQueries.Count.ShouldBe(0);
        spec.OrderByDescendingQueries.Count.ShouldBe(1);
    }

    #endregion
}
