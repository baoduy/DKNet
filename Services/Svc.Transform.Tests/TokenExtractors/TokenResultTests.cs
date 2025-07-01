
using DKNet.Svc.Transformation.Exceptions;
using DKNet.Svc.Transformation.TokenDefinitions;
using DKNet.Svc.Transformation.TokenExtractors;

namespace Svc.Transform.Tests.TokenExtractors;

[TestClass]
public class TokenResultTests
{
 

    [TestMethod]
    public void CreateTokenResult()
    {
        var t = new TokenResult(new CurlyBracketDefinition(), "{A}", "123 {A}", 4);

        t.Definition.ShouldBeOfType<CurlyBracketDefinition>();
        t.Key.ShouldBe("A");
        t.Key.ShouldBe("A");
        t.Index.ShouldBe(4);
        t.OriginalString.ShouldBe("123 {A}");
    }

    [TestMethod]
    public void CreateCustomTokenResult()
    {
        var t = new TokenResult(new TokenDefinition("{{", "}}"), "{{A}}", "123 {{A}}", 4);

        t.Definition.ShouldBeOfType<TokenDefinition>();
        t.Key.ShouldBe("A");
        t.Key.ShouldBe("A");
        t.Index.ShouldBe(4);
        t.OriginalString.ShouldBe("123 {{A}}");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void CreateTokenResultDefinitionIsNull()
    {
        new TokenResult(definition: null, "[A]", "123 [A]", 1);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void CreateTokenResultInCorrectIndex()
    {
        new TokenResult(new CurlyBracketDefinition(), "{A}", "123 {A}", -1);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void CreateTokenResultInCorrectIndex2()
    {
        new TokenResult(new CurlyBracketDefinition(), "{A}", "123 {A}", 100);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidTokenException))]
    public void CreateTokenResultInCorrectToken()
    {
        new TokenResult(new CurlyBracketDefinition(), "{A", "123 {A}", 1);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void CreateTokenResultOriginalStringIsNull()
    {
        new TokenResult(new SquareBracketDefinition(), "[A]", originalString: null, 1);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void CreateTokenResultTokenIsNull()
    {
        new TokenResult(new SquareBracketDefinition(), token: null, "123 [A]", 1);
    }
}