
using DKNet.Svc.Transformation.TokenDefinitions;
using DKNet.Svc.Transformation.TokenExtractors;
using DKNet.Svc.Transformation.TokenResolvers;

namespace Svc.Transform.Tests.TokenResolvers;

[TestClass]
public class TokenResolversTests
{


    [TestMethod]
    public void TestTokenResolver()
    {
        var resolver = new TokenResolver();

        var val = resolver.Resolve(new TokenResult(new CurlyBracketDefinition(), "{A}", "{A} 123", 0), [
            null,
            new {A = (string) null},
            new {A = 123},
        ]);

        val.ShouldBe(123);
    }

    [TestMethod]
    public async Task TestTokenResolverAsync()
    {
        var resolver = new TokenResolver();

        var val = await resolver.ResolveAsync(new TokenResult(new CurlyBracketDefinition(), "{A}", "{A} 123", 0), [
            null,
            new {A = (string) null},
            new {A = 123},
        ]);

        val.ShouldBe(123);
    }

    // [TestMethod]
    // [ExpectedException(typeof(ArgumentNullException))]
    // public void TestTokenResolverDataEmpty()
    // {
    //     var resolver = new TokenResolver();

    //     resolver.Resolve(new TokenResult(new CurlyBracketDefinition(), "{A}", "{A} 123", 0), []);
    // }

    // [TestMethod]
    // [ExpectedException(typeof(ArgumentNullException))]
    // public void TestTokenResolverDataNull()
    // {
    //     var resolver = new TokenResolver();

    //     resolver.Resolve(new TokenResult(new CurlyBracketDefinition(), "{A}", "{A} 123", 0), null);
    // }

    [TestMethod]
    public async Task TestTokenResolverDictionary()
    {
        var resolver = new TokenResolver();

        var val = await resolver.ResolveAsync(new TokenResult(new CurlyBracketDefinition(), "{A}", "{A} 123", 0), new Dictionary<string, object>
        (StringComparer.Ordinal)
        {
            {"A", "Duy"},
        });

        val.ShouldBe("Duy");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public async Task TestTokenResolverOnlyDictionaryStringObjectIsSupportted()
    {
        var resolver = new TokenResolver();

        var val = await resolver.ResolveAsync(new TokenResult(new CurlyBracketDefinition(), "{A}", "{A} 123", 0), new Dictionary<object, object>
        {
            {"A", "Duy"},
        });

        val.ShouldBe("Duy");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void TestTokenResolverTokenNull()
    {
        var resolver = new TokenResolver();

        resolver.Resolve(null, [
             null,
            new {A = (string) null},
            new {A = 123},
         ]);
    }

 
}