
using DKNet.Svc.Transformation.TokenDefinitions;

namespace Svc.Transform.Tests.TokenDefinitions;

[TestClass]
public class BracesTokenDefinitionTests
{


    [TestMethod]
    public void BracesTokenDefinitionTest()
    {
        var t = new SquareBracketDefinition();

        t.IsToken("[Duy]")
            .ShouldBeTrue();

        t.IsToken("{Duy}")
            .ShouldBeFalse();

        t.IsToken("<Duy")
            .ShouldBeFalse();

        t.IsToken("Duy>")
            .ShouldBeFalse();
    }


}