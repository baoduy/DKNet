namespace Svc.Transform.Tests.TokenDefinitions;

public class AngledBracketTokenDefinitionTests
{
    #region Methods

    [Fact]
    public void AngledBracketTokenDefinitionTest()
    {
        var t = TransformOptions.AngledBrackets;

        t.IsToken("<Duy>")
            .ShouldBeTrue();

        t.IsToken("[Duy]")
            .ShouldBeFalse();

        t.IsToken("<Duy")
            .ShouldBeFalse();

        t.IsToken("Duy>")
            .ShouldBeFalse();
    }

    #endregion
}