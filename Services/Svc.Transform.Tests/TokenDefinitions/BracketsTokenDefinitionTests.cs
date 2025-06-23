
using DKNet.Svc.Transformation.TokenDefinitions;

namespace Svc.Transform.Tests.TokenDefinitions;

[TestClass]
public class BracketsTokenDefinitionTests
{


    [TestMethod]
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