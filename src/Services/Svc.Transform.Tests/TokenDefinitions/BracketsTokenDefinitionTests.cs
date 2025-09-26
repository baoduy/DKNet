namespace Svc.Transform.Tests.TokenDefinitions;

public class BracketsTokenDefinitionTests
{
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
}