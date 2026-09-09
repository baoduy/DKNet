using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Covers the recent-activity window's parameterization shape (DRK-1166 R11): both bound values must be
///     read through a mutable placeholder member access, never baked into the expression tree as literals — a
///     default window derived from "now" differs on every request, and an inlined literal would put a fresh
///     query plan in the server plan cache per request.
/// </summary>
/// <remarks>
///     Asserted via <see cref="Expression.ToString()" /> rather than <c>ToQueryString()</c> against a real
///     provider (the pattern <see cref="ListQuerySearchParameterizationTests" /> uses for the search predicate):
///     Microsoft.Data.Sqlite 10 changed its <see cref="DateTimeOffset" /> handling and cannot translate a
///     relational comparison on it at all (verified against this repo's pinned 10.0.11 — even a bare
///     <c>x.CreatedOn &gt;= someNullableValue</c> throws <see cref="InvalidOperationException" /> "could not be
///     translated"), so a Sqlite round trip would fail for reasons unrelated to this feature. A closed
///     expression tree's own <see cref="object.ToString" /> already reveals whether a value node is a literal
///     or a member access — no database is needed to tell the two apart, and
///     <c>value(...ActivityWindowBox).From</c> in the printed tree is exactly the shape a literal could never
///     produce.
/// </remarks>
public sealed class ActivityWindowParameterizationTests
{
    #region Methods

    [Fact]
    public void DefaultWindow_ReadsItsBoundThroughTheBox_NotAnInlinedLiteral()
    {
        var valid = ListQuery.TryValidate<OrderEntity, OrderModel>(
            new ListQueryRequest(), new ListQueryOptions(), out var query, out var error);

        valid.ShouldBeTrue();
        error.ShouldBeNull();
        query.ShouldNotBeNull();
        query.Filter.ShouldNotBeNull();

        var text = query.Filter.ToString();
        text.ShouldContain("value(DKNet.AspCore.Extensions.Endpoints.ActivityWindowBox).From");
    }

    [Fact]
    public void CallerSuppliedBounds_AlsoReadThroughTheBox_NotInlinedLiterals()
    {
        var valid = ListQuery.TryValidate<OrderEntity, OrderModel>(
            new ListQueryRequest { FromDate = DateTimeOffset.UtcNow.AddYears(-1), ToDate = DateTimeOffset.UtcNow },
            new ListQueryOptions(),
            out var query,
            out var error);

        valid.ShouldBeTrue();
        error.ShouldBeNull();
        query.ShouldNotBeNull();
        query.Filter.ShouldNotBeNull();

        var text = query.Filter.ToString();
        text.ShouldContain("value(DKNet.AspCore.Extensions.Endpoints.ActivityWindowBox).From");
        text.ShouldContain("value(DKNet.AspCore.Extensions.Endpoints.ActivityWindowBox).To");
    }

    [Fact]
    public void TwoRequestsWithDifferentBounds_ProduceByteIdenticalPredicateShapes()
    {
        // Same (TModel, TEntity) pair: both calls must reuse the cached template rather than each rebuilding
        // its own tree, so the printed shape — which never contains a bound's actual value (see remarks above)
        // — is identical regardless of what the two requests actually asked for.
        ListQuery.TryValidate<OrderEntity, OrderModel>(
            new ListQueryRequest { FromDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            new ListQueryOptions(),
            out var query1,
            out _);
        ListQuery.TryValidate<OrderEntity, OrderModel>(
            new ListQueryRequest { FromDate = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            new ListQueryOptions(),
            out var query2,
            out _);

        query1!.Filter!.ToString().ShouldBe(query2!.Filter!.ToString());
    }

    #endregion
}
