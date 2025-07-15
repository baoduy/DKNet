using DKNet.Svc.Transformation.TokenDefinitions;

namespace Svc.Transform.Tests.TokenDefinitions;

public class BracketsTokenDefinitionTests
{
    [Fact]
    public void BracketsTokenDefinitionTest()
    {
        var t = new CurlyBracketDefinition();

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