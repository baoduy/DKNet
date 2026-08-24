using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Specifications.Dynamics;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Covers <see cref="ListFilter" />'s <see cref="IParsable{TSelf}" /> implementation — the contract that lets
///     minimal APIs bind a repeated <c>?filter=…</c> query parameter into a typed array. Anything
///     <see cref="ListFilter.TryParse" /> rejects never reaches the endpoint handler, so this parser decides
///     which malformed requests become a binding-level 400 and which reach the endpoint's own validation.
/// </summary>
public class ListFilterTests
{
    #region Methods

    [Fact]
    public void TryParse_WellFormedFilter_SplitsIntoFieldOperationAndValue()
    {
        ListFilter.TryParse("status:Equal:Pending", null, out var filter).ShouldBeTrue();

        filter.Field.ShouldBe("status");
        filter.Operation.ShouldBe(Ops.Equal);
        filter.Value.ShouldBe("Pending");
    }

    [Fact]
    public void TryParse_ValueContainingSeparators_KeepsTheWholeValue()
    {
        // The reason parsing splits on the first two separators only: an ISO-8601 instant carries two of its
        // own, and a naive split would truncate the value to "2026-01-01T10" and filter on the wrong thing.
        ListFilter.TryParse("createdOn:GreaterThan:2026-01-01T10:30:00Z", null, out var filter).ShouldBeTrue();

        filter.Field.ShouldBe("createdOn");
        filter.Operation.ShouldBe(Ops.GreaterThan);
        filter.Value.ShouldBe("2026-01-01T10:30:00Z");
    }

    [Theory]
    [InlineData("in")]
    [InlineData("In")]
    [InlineData("IN")]
    public void TryParse_OperationInAnyCasing_Resolves(string operation)
    {
        ListFilter.TryParse($"status:{operation}:Pending,Completed", null, out var filter).ShouldBeTrue();

        filter.Operation.ShouldBe(Ops.In);
        filter.Value.ShouldBe("Pending,Completed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("status")]
    [InlineData("status:Equal")]
    [InlineData(":Equal:Pending")]
    [InlineData("status:Frobnicate:Pending")]
    public void TryParse_UnusableFilter_Fails(string? text)
    {
        ListFilter.TryParse(text, null, out var filter).ShouldBeFalse();

        filter.ShouldBe(default(ListFilter));
    }

    [Fact]
    public void Parse_UnusableFilter_ThrowsWithTheValidOperations()
    {
        // Parse is what a caller reaches for outside the binding pipeline, so its message has to be actionable
        // on its own — the binding path has only the parameter name to work with.
        var exception = Should.Throw<FormatException>(() => ListFilter.Parse("status:Frobnicate:Pending", null));

        exception.Message.ShouldContain(nameof(Ops.Equal));
        exception.Message.ShouldContain(nameof(Ops.In));
    }

    [Fact]
    public void ToString_RoundTripsThroughTryParse()
    {
        var original = new ListFilter("createdOn", Ops.LessThanOrEqual, "2026-01-01T10:30:00Z");

        ListFilter.TryParse(original.ToString(), null, out var reparsed).ShouldBeTrue();

        reparsed.ShouldBe(original);
    }

    #endregion
}
