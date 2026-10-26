using DKNet.Svc.Transformation.Exceptions;
using Microsoft.Extensions.Options;

namespace Svc.Transform.Tests.TokenResolvers;

public class TokenResolverIndexerTests
{
    #region Fields

    private static readonly TokenResult ItemToken = new(TransformOptions.SquareBrackets, "[Item]", "[Item]", 0);

    #endregion

    #region Methods

    [Fact]
    public void Resolve_IndexerOnlyItemInsideList_FallsThroughToNextItem()
    {
        var resolver = new TokenResolver();

        var val = resolver.Resolve(
            ItemToken,
            new List<object> { new Dictionary<string, object> { ["A"] = "x" }, new { Item = "FromObj" } });

        val.ShouldBe("FromObj");
    }

    [Fact]
    public void Resolve_PlainItemProperty_IsStillRead()
    {
        var resolver = new TokenResolver();

        var val = resolver.Resolve(ItemToken, new { Item = "X" });

        val.ShouldBe("X");
    }

    [Fact]
    public void Transform_IndexerOnlyItemInGlobalParameters_IsUnResolved()
    {
        var t = new TransformerService(Options.Create(new TransformOptions
        {
            GlobalParameters = [new Dictionary<string, object> { ["A"] = "x" }]
        }));

        Should.Throw<UnResolvedTokenException>(() => t.Transform("[Item]"));
    }

    [Fact]
    public void Transform_IndexerOnlyItemWithLeaveAsIs_LeavesTokenInPlace()
    {
        var t = new TransformerService(Options.Create(new TransformOptions
        {
            TokenNotFoundBehavior = TokenNotFoundBehavior.LeaveAsIs
        }));

        var rs = t.Transform("Hi [Item]", new List<object> { new Dictionary<string, object> { ["A"] = "x" } });

        rs.ShouldBe("Hi [Item]");
    }

    #endregion
}
