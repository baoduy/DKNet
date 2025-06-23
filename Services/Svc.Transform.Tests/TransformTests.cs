using DKNet.Svc.Transformation;
using DKNet.Svc.Transformation.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Svc.Transform.Tests;

[TestClass]
public class TransformTests
{
    [TestMethod]
    public async Task TransformHugeTemplateAsyncDisableCacheTest()
    {
        var d = Path.GetDirectoryName(typeof(TransformTests).Assembly.Location);
        var template = await File.ReadAllTextAsync(d + "/TestData/Data.txt");

        var t = new TransformerService(new TransformOptions { DisabledLocalCache = true });
        var s = await t.TransformAsync(template, new { A = "Hoang", B = "Bao", C = "Duy", D = "DKNet" });

        s.ShouldContain("Hoang"); // "Hoang", "Bao", "Duy", "DKNet"
        s.ShouldNotContain("{");
        s.ShouldNotContain("[");
        s.ShouldNotContain("<");
    }

    [TestMethod]
    public async Task TransformHugeTemplateAsyncDataProviderTest()
    {
        var d = Path.GetDirectoryName(typeof(TransformTests).Assembly.Location);
        var template = await File.ReadAllTextAsync(d + "/TestData/Data.txt");

        var t = new TransformerService(new TransformOptions());
        var s = await t.TransformAsync(template, token => Task.FromResult("Duy" as object));

        s.ShouldContain("Duy");
        s.ShouldNotContain("{");
        s.ShouldNotContain("[");
        s.ShouldNotContain("<");
    }

    [TestMethod]
    public async Task TransformHugeTemplateAsyncTest()
    {
        var d = Path.GetDirectoryName(typeof(TransformTests).Assembly.Location);
        var template = await File.ReadAllTextAsync(d + "/TestData/Data.txt");

        var t = new TransformerService(new TransformOptions());
        var s = await t.TransformAsync(template, new { A = "Hoang", B = "Bao", C = "Duy", D = "DKNet" });

        s.ShouldContain("Bao");
        s.ShouldNotContain("{");
        s.ShouldNotContain("[");
        s.ShouldNotContain("<");
    }

    [TestMethod]
    public async Task TransformAsyncDefaultDataTest()
    {
        var t = new TransformerService(new TransformOptions { GlobalParameters = [new { A = "Bao" }] });
        var s = await t.TransformAsync("Hoang [A] Duy");
        s.ShouldBe("Hoang Bao Duy");
    }

    [TestMethod]
    public async Task TransformAsyncTest()
    {
        var t = new TransformerService(new TransformOptions());
        var s = await t.TransformAsync("Hoang [A] Duy", new { A = "Bao" });
        s.ShouldBe("Hoang Bao Duy");
    }


    [TestMethod]
    public async Task TransformAsyncCustomTest()
    {
        var t = new TransformerService(new TransformOptions());
        var s = await t.TransformAsync("Hoang [[A]] Duy", new { A = "Bao" });
        s.ShouldBe("Hoang [Bao] Duy");
    }

    [TestMethod]
    [ExpectedException(typeof(UnResolvedTokenException))]
    public async Task TestUnResolvedTokenException()
    {
        var service = new ServiceCollection()
            .AddTransformerService()
            .BuildServiceProvider();

        var transformer = service.GetRequiredService<ITransformerService>();
        await transformer.TransformAsync("{A}", "{A} 123", new Dictionary<string, object>
            (StringComparer.OrdinalIgnoreCase)
            {
                { "B", "Duy" },
            });
    }
}