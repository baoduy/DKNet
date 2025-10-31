using DKNet.AspCore.SlimBus;
using Shouldly;
using X.PagedList;

namespace AspCore.SlimBus.Tests;

public class PagedResultTests
{
    #region Methods

    [Fact]
    public void Constructor_WithPagedList_ShouldMapProperties()
    {
        var list = new StaticPagedList<string>(["a", "b"], 2, 1, 2);
        var pagedResult = new PagedResult<string>(list);
        pagedResult.PageNumber.ShouldBe(2);
        pagedResult.PageSize.ShouldBe(1);
        pagedResult.PageCount.ShouldBe(2);
        pagedResult.TotalItemCount.ShouldBe(2);
        pagedResult.Items.SequenceEqual(list).ShouldBeTrue();
    }

    [Fact]
    public void DefaultConstructor_ShouldInitializeEmptyItems()
    {
        var pagedResult = new PagedResult<string>();
        pagedResult.Items.ShouldNotBeNull();
        pagedResult.Items.Count.ShouldBe(0);
    }

    #endregion
}