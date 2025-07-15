using DKNet.Svc.Transformation.TokenDefinitions;

namespace Svc.Transform.Tests.TokenDefinitions;

public class AngledBracketTokenDefinitionTests
{
    [Fact]
    public void AngledBracketTokenDefinitionTest()
    {
        var t = new AngledBracketDefinition();

        t.IsToken("<Duy>")
            .ShouldBeTrue();

        t.IsToken("[Duy]")
            .ShouldBeFalse();

        t.IsToken("<Duy")
            .ShouldBeFalse();

        t.IsToken("Duy>")
            .ShouldBeFalse();
    }
}