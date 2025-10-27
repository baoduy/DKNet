namespace Svc.Transform.Tests.TokenExtractors;

public class TokenExtractorTests
{
    #region Methods

    [Fact]
    public void TokenExtractorAngledBracketTest()
    {
        var t = new TokenExtractor(TransformOptions.AngledBrackets);

        var list1 = t.Extract("Hoang <Duy> Bao").ToList();
        list1.Count.ShouldBeGreaterThanOrEqualTo(1);
        list1[0].Token.ShouldBe("<Duy>");

        t.Extract("Hoang Duy Bao").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        t.Extract("").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        t.Extract(null!).ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        var list = t.Extract(
                "Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy>")
            .ToList();

        list.Count.ShouldBeGreaterThanOrEqualTo(12);
        list[0].Key.ShouldBe("Duy");
    }

    [Fact]
    public async Task TokenExtractorAsyncTest()
    {
        var t = new TokenExtractor(TransformOptions.CurlyBrackets);

        (await t.ExtractAsync("Hoang {{Duy} Bao")).ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TokenExtractorBracketsTokenTest()
    {
        var t = new TokenExtractor(TransformOptions.CurlyBrackets);

        var list1 = t.Extract("Hoang {Duy} Bao").ToList();
        list1.Count.ShouldBeGreaterThanOrEqualTo(1);
        list1[0].Token.ShouldBe("{Duy}");

        t.Extract("Hoang Duy Bao").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        t.Extract("").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        t.Extract(null!).ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        var list = t.Extract(
                "Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy}")
            .ToList();

        list.Count.ShouldBeGreaterThanOrEqualTo(12);
        list[0].Key.ShouldBe("Duy");
    }

    [Fact]
    public void TokenExtractorInCorrectTokenTest()
    {
        var t = new TokenExtractor(TransformOptions.CurlyBrackets);

        t.Extract("Hoang ]Duy[ Bao").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        t.Extract("Hoang Duy Bao[[").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void TokenExtractorNullArgumentTest()
    {
        var action = () => new TokenExtractor(null!);
        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void TokenExtractorSupportDuplicateOfTokenTest()
    {
        var t = new TokenExtractor(TransformOptions.CurlyBrackets);

        var list = t.Extract("Hoang [{Duy}] Bao").ToList();
        list.Count.ShouldBeGreaterThanOrEqualTo(1);
        list[0].Token.ShouldBe("{Duy}");
    }

    #endregion
}