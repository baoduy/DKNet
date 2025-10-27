namespace Svc.Transform.Tests.TokenDefinitions;

public class BracketsTokenDefinitionTests
{
    #region Methods

    [Fact]
    public void BracketsTokenDefinitionTest()
    {
        var t = TransformOptions.CurlyBrackets;

        t.IsToken("{Duy}")
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