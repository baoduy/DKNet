
using DKNet.Svc.Transformation.Exceptions;
using DKNet.Svc.Transformation.TokenDefinitions;
using DKNet.Svc.Transformation.TokenExtractors;

namespace Svc.Transform.Tests.TokenExtractors;


public class TokenResultTests
{
 

    [Fact]
    public void CreateTokenResult()
    {
        var t = new TokenResult(new CurlyBracketDefinition(), "{A}", "123 {A}", 4);

        t.Definition.ShouldBeOfType<CurlyBracketDefinition>();
        t.Key.ShouldBe("A");
        t.Key.ShouldBe("A");
        t.Index.ShouldBe(4);
        t.OriginalString.ShouldBe("123 {A}");
    }

    [Fact]
    public void CreateCustomTokenResult()
    {
        var t = new TokenResult(new TokenDefinition("{{", "}}"), "{{A}}", "123 {{A}}", 4);

        t.Definition.ShouldBeOfType<TokenDefinition>();
        t.Key.ShouldBe("A");
        t.Key.ShouldBe("A");
        t.Index.ShouldBe(4);
        t.OriginalString.ShouldBe("123 {{A}}");
    }

    [Fact]
    public void CreateTokenResultDefinitionIsNull()
    {
        new TokenResult(definition: null, "[A]", "123 [A]", 1);
    }

    [Fact]
    public void CreateTokenResultInCorrectIndex()
    {
        new TokenResult(new CurlyBracketDefinition(), "{A}", "123 {A}", -1);
    }

    [Fact]
    public void CreateTokenResultInCorrectIndex2()
    {
        new TokenResult(new CurlyBracketDefinition(), "{A}", "123 {A}", 100);
    }

    [Fact]
    public void CreateTokenResultInCorrectToken()
    {
        new TokenResult(new CurlyBracketDefinition(), "{A", "123 {A}", 1);
    }

    [Fact]
    public void CreateTokenResultOriginalStringIsNull()
    {
        new TokenResult(new SquareBracketDefinition(), "[A]", originalString: null, 1);
    }

    [Fact]
    public void CreateTokenResultTokenIsNull()
    {
        new TokenResult(new SquareBracketDefinition(), token: null, "123 [A]", 1);
    }
}