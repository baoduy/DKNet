using DKNet.Svc.Transformation.TokenDefinitions;
using DKNet.Svc.Transformation.TokenExtractors;

namespace Svc.Transform.Tests.TokenExtractors;

[TestClass]
public class TokenExtractorTests
{
    [TestMethod]
    public void TokenExtractorAngledBracketTest()
    {
        var t = new TokenExtractor(new AngledBracketDefinition());

        var list1 = t.Extract("Hoang <Duy> Bao").ToList();
        list1.Count.ShouldBeGreaterThanOrEqualTo(1);
        list1.First().Token.ShouldBe("<Duy>");

        t.Extract("Hoang Duy Bao").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        t.Extract("").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        t.Extract(null).ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        var list = t.Extract(
                "Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy> Bao, Hoang <Duy>")
            .ToList();

        list.Count.ShouldBeGreaterThanOrEqualTo(12);
        list.First().Key.ShouldBe("Duy");
    }

    [TestMethod]
    public async Task TokenExtractorAsyncTest()
    {
        var t = new TokenExtractor(new CurlyBracketDefinition());

        (await t.ExtractAsync("Hoang {{Duy} Bao")).ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TokenExtractorBracketsTokenTest()
    {
        var t = new TokenExtractor(new CurlyBracketDefinition());

        var list1 = t.Extract("Hoang {Duy} Bao").ToList();
        list1.Count.ShouldBeGreaterThanOrEqualTo(1);
        list1.First().Token.ShouldBe("{Duy}");

        t.Extract("Hoang Duy Bao").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        t.Extract("").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        t.Extract(null).ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        var list = t.Extract(
                "Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy} Bao, Hoang {Duy}")
            .ToList();

        list.Count.ShouldBeGreaterThanOrEqualTo(12);
        list.First().Key.ShouldBe("Duy");
    }

    [TestMethod]
    public void TokenExtractorInCorrectTokenTest()
    {
        var t = new TokenExtractor(new CurlyBracketDefinition());

        t.Extract("Hoang ]Duy[ Bao").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);

        t.Extract("Hoang Duy Bao[[").ToList()
            .Count.ShouldBeGreaterThanOrEqualTo(0);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void TokenExtractorNullArgumentTest()
    {
        new TokenExtractor(null);
    }

    [TestMethod]
    public void TokenExtractorSupportDuplicateOfTokenTest()
    {
        var t = new TokenExtractor(new CurlyBracketDefinition());

        var list = t.Extract("Hoang [{Duy}] Bao").ToList();
        list.Count.ShouldBeGreaterThanOrEqualTo(1);
        list.First().Token.ShouldBe("{Duy}");
    }
}