using DKNet.AspCore.Extensions;
using Shouldly;
using X.PagedList;

namespace AspCore.Extensions.Tests;

public class PagedResponseTests
{
    #region Methods

    [Fact]
    public void Constructor_WithPagedList_ShouldMapProperties()
    {
        var list = new StaticPagedList<string>(["a", "b"], 2, 1, 2);
        var pagedResult = new PagedResponse<string>(list);
        pagedResult.PageNumber.ShouldBe(2);
        pagedResult.PageSize.ShouldBe(1);
        pagedResult.PageCount.ShouldBe(2);
        pagedResult.TotalItemCount.ShouldBe(2);
        pagedResult.Items.SequenceEqual(list).ShouldBeTrue();
    }

    [Fact]
    public void DefaultConstructor_ShouldInitializeEmptyItems()
    {
        var pagedResult = new PagedResponse<string>();
        pagedResult.Items.ShouldNotBeNull();
        pagedResult.Items.Count.ShouldBe(0);
    }

    #endregion
}