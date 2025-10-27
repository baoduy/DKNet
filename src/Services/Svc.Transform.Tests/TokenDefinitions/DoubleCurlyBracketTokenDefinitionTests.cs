namespace Svc.Transform.Tests.TokenDefinitions;

public class DoubleCurlyBracketTokenDefinitionTests
{
    #region Methods

    [Fact]
    public void BeginTag_ShouldBeDoubleCurlyBraces()
    {
        var def = TransformOptions.DoubleCurlyBrackets;
        Assert.Equal("{{", def.BeginTag);
    }

    [Fact]
    public void EndTag_ShouldBeDoubleCurlyBraces()
    {
        var def = TransformOptions.DoubleCurlyBrackets;
        Assert.Equal("}}", def.EndTag);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{token}")]
    [InlineData("{{token}")]
    [InlineData("token}}")]
    [InlineData("{{token}}}")]
    [InlineData("{{token}")]
    [InlineData("{{}}")]
    public void IsToken_ShouldReturnFalse_ForInvalidTokens(string value)
    {
        var def = TransformOptions.DoubleCurlyBrackets;
        def.IsToken(value).ShouldBeFalse(value);
    }

    [Theory]
    [InlineData("{{token}}")]
    [InlineData("{{TOKEN}}")]
    [InlineData("{{123}}")]
    public void IsToken_ShouldReturnTrue_ForValidTokens(string value)
    {
        var def = TransformOptions.DoubleCurlyBrackets;
        Assert.True(def.IsToken(value));
    }

    #endregion
}