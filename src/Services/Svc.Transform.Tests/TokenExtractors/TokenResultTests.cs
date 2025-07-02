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
       var action =()=> new TokenResult(definition: null, "[A]", "123 [A]", 1);
       action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void CreateTokenResultInCorrectIndex()
    {
        var action = ()=>new TokenResult(new CurlyBracketDefinition(), "{A}", "123 {A}", -1);
        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateTokenResultInCorrectIndex2()
    {
        var action = ()=>new TokenResult(new CurlyBracketDefinition(), "{A}", "123 {A}", 100);
        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateTokenResultInCorrectToken()
    {
        var action = ()=>new TokenResult(new CurlyBracketDefinition(), "{A", "123 {A}", 1);
        action.ShouldThrow<InvalidTokenException>();
    }

    [Fact]
    public void CreateTokenResultOriginalStringIsNull()
    {
        var action = ()=>new TokenResult(new SquareBracketDefinition(), "[A]", originalString: null, 1);
        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void CreateTokenResultTokenIsNull()
    {
        var action = ()=>new TokenResult(new SquareBracketDefinition(), token: null, "123 [A]", 1);
        action.ShouldThrow<ArgumentNullException>();
    }
}