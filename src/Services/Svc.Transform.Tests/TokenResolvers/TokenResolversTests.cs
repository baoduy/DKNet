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

    [Fact]
    public void TestTokenResolverTokenNull()
    {
        var resolver = new TokenResolver();

        var action = () => resolver.Resolve(null!, null, new { A = (string)null! }, new { A = 123 });

        action.ShouldThrow<ArgumentNullException>();
    }

    #endregion
}