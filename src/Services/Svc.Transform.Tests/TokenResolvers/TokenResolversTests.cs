namespace Svc.Transform.Tests.TokenResolvers;

public class TokenResolversTests
{
    #region Methods

    [Fact]
    public void TestTokenResolver()
    {
        var resolver = new TokenResolver();

        var val = resolver.Resolve(
            new TokenResult(TransformOptions.CurlyBrackets, "{A}", "{A} 123", 0),
            null,
            new { A = (string)null! },
            new { A = 123 });

        val.ShouldBe(123);
    }

    [Fact]
    public async Task TestTokenResolverAsync()
    {
        var resolver = new TokenResolver();

        var val = await resolver.ResolveAsync(
            new TokenResult(TransformOptions.CurlyBrackets, "{A}", "{A} 123", 0),
            null,
            new { A = (string)null! },
            new { A = 123 });

        val.ShouldBe(123);
    }

    // [Fact]
    // public void TestTokenResolverDataEmpty()
    // {
    //     var resolver = new TokenResolver();

    //     resolver.Resolve(new TokenResult(TransformOptions.CurlyBrackets, "{A}", "{A} 123", 0), []);
    // }

    // [Fact]
    // public void TestTokenResolverDataNull()
    // {
    //     var resolver = new TokenResolver();

    //     resolver.Resolve(new TokenResult(TransformOptions.CurlyBrackets, "{A}", "{A} 123", 0), null);
    // }

    [Fact]
    public async Task TestTokenResolverDictionary()
    {
        var resolver = new TokenResolver();

        var val = await resolver.ResolveAsync(
            new TokenResult(TransformOptions.CurlyBrackets, "{A}", "{A} 123", 0),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "A", "Duy" }
            });

        val.ShouldBe("Duy");
    }

    [Fact]
    public async Task TestTokenResolverOnlyDictionaryStringObjectIsSupported()
    {
        var resolver = new TokenResolver();

        var val = await resolver.ResolveAsync(
            new TokenResult(TransformOptions.CurlyBrackets, "{A}", "{A} 123", 0),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "A", "Duy" }
            });

        val.ShouldBe("Duy");
    }

    private static readonly TokenResult CompanyToken =
        new(TransformOptions.SquareBrackets, "[Company]", "[Company]", 0);

    [Fact]
    public void Resolve_DictionaryInsideNestedList_ResolvesByKey()
    {
        var resolver = new TokenResolver();
        var dict = new Dictionary<string, string> { ["Company"] = "Acme" };

        var val = resolver.Resolve(CompanyToken, new List<object> { new List<object> { dict } });

        val.ShouldBe("Acme");
    }

    [Fact]
    public void Resolve_DictionaryInsideList_NeverAnswersFromItsOwnProperties()
    {
        var resolver = new TokenResolver();
        var dict = new Dictionary<string, string> { ["Company"] = "Acme" };

        var val = resolver.Resolve(
            new TokenResult(TransformOptions.SquareBrackets, "[Count]", "[Count]", 0),
            new List<object> { dict });

        val.ShouldBeNull();
    }

    [Fact]
    public void Resolve_OrdinalDictionaryInsideList_ResolvesKeyCaseInsensitively()
    {
        var resolver = new TokenResolver();
        var dict = new Dictionary<string, string>(StringComparer.Ordinal) { ["company"] = "Acme" };

        var val = resolver.Resolve(CompanyToken, new List<object> { dict });

        val.ShouldBe("Acme");
    }

    [Fact]
    public void Resolve_DictionaryInsideListWithoutTheKey_FallsThroughToNextItem()
    {
        var resolver = new TokenResolver();
        var dict = new Dictionary<string, string> { ["Other"] = "Acme" };

        var val = resolver.Resolve(CompanyToken, new List<object> { dict, new { Company = "FromObj" } });

        val.ShouldBe("FromObj");
    }

    [Fact]
    public void Resolve_DictionaryInsideListWithNullValue_FallsThroughToNextItem()
    {
        var resolver = new TokenResolver();
        var dict = new Dictionary<string, string?> { ["Company"] = null };

        var val = resolver.Resolve(CompanyToken, new List<object> { dict, new { Company = "FromObj" } });

        val.ShouldBe("FromObj");
    }

    [Fact]
    public void Resolve_DictionaryBeforeObjectInsideList_FirstNonNullWins()
    {
        var resolver = new TokenResolver();
        var dict = new Dictionary<string, string> { ["Company"] = "Acme" };

        var val = resolver.Resolve(CompanyToken, new List<object> { dict, new { Company = "FromObj" } });

        val.ShouldBe("Acme");
    }

    [Fact]
    public void Resolve_StringObjectDictionaryInsideList_IsReadByPropertyWithoutThrowing()
    {
        var resolver = new TokenResolver();
        var dict = new Dictionary<string, object> { ["Company"] = "Acme" };

        var val = resolver.Resolve(CompanyToken, new List<object> { dict, new { Company = "FromObj" } });

        val.ShouldBe("FromObj");
    }

    [Fact]
    public void Resolve_EmptyListOrListOfNull_ReturnsNull()
    {
        var resolver = new TokenResolver();

        resolver.Resolve(CompanyToken, new List<object>()).ShouldBeNull();
        resolver.Resolve(CompanyToken, new List<object?> { null }).ShouldBeNull();
    }

    [Fact]
    public void Resolve_DictionaryThatAlsoEnumeratesObjectsInsideList_ResolvesByKeyFirst()
    {
        var resolver = new TokenResolver();
        var dict = new DictionaryEnumeratingObjects { ["Company"] = "Acme" };

        var val = resolver.Resolve(CompanyToken, new List<object> { dict });

        val.ShouldBe("Acme");
    }

    private sealed class DictionaryEnumeratingObjects : Dictionary<string, string>, IEnumerable<object?>
    {
        IEnumerator<object?> IEnumerable<object?>.GetEnumerator()
        {
            yield return new { Company = "FromEnumeration" };
        }
    }

    [Fact]
    public void TestTokenResolverTokenNull()
    {
        var resolver = new TokenResolver();

        var action = () => resolver.Resolve(null!, null, new { A = (string)null! }, new { A = 123 });

        action.ShouldThrow<ArgumentNullException>();
    }

    #endregion
}