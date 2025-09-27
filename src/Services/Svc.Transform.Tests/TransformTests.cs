using DKNet.Svc.Transformation.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Svc.Transform.Tests;

public class TransformTests
{
    [Fact]
    public async Task TransformHugeTemplateAsyncDisableCacheTest()
    {
        var d = Path.GetDirectoryName(typeof(TransformTests).Assembly.Location);
        var template = await File.ReadAllTextAsync(d + "/TestData/Data.txt");

        var options = Options.Create(new TransformOptions
        {
            DefaultDefinitions =
            [
                TransformOptions.AngledBrackets,
                TransformOptions.CurlyBrackets,
                TransformOptions.SquareBrackets
            ]
        });
        var t = new TransformerService(options);
        var s = await t.TransformAsync(template, new { A = "Hoang", B = "Bao", C = "Duy", D = "DKNet" });

        s.ShouldContain("Hoang");
        s.ShouldNotContain("{");
        s.ShouldNotContain("[");
        s.ShouldNotContain("<");
    }


    [Fact]
    public async Task TransformHugeTemplateAsyncTest()
    {
        var d = Path.GetDirectoryName(typeof(TransformTests).Assembly.Location);
        var template = await File.ReadAllTextAsync(d + "/TestData/Data.txt");

        var options = Options.Create(new TransformOptions
        {
            DefaultDefinitions =
            [
                TransformOptions.AngledBrackets,
                TransformOptions.CurlyBrackets,
                TransformOptions.SquareBrackets
            ]
        });
        var t = new TransformerService(options);
        var s = await t.TransformAsync(template, new { A = "Hoang", B = "Bao", C = "Duy", D = "DKNet" });

        s.ShouldContain("Bao");
        s.ShouldNotContain("{");
        s.ShouldNotContain("[");
        s.ShouldNotContain("<");
    }

    [Fact]
    public async Task TransformAsyncDefaultDataTest()
    {
        var options = Options.Create(new TransformOptions { GlobalParameters = [new { A = "Bao" }] });
        var t = new TransformerService(options);
        var s = await t.TransformAsync("Hoang [A] Duy");
        s.ShouldBe("Hoang Bao Duy");
    }

    [Fact]
    public async Task TransformAsyncTest()
    {
        var options = Options.Create(new TransformOptions());
        var t = new TransformerService(options);
        var s = await t.TransformAsync("Hoang [A] Duy", new { A = "Bao" });
        s.ShouldBe("Hoang Bao Duy");
    }


    [Fact]
    public async Task TransformAsyncCustomTest()
    {
        var options = Options.Create(new TransformOptions());
        var t = new TransformerService(options);
        var s = await t.TransformAsync("Hoang [[A]] Duy", new { A = "Bao" });
        s.ShouldBe("Hoang [Bao] Duy");
    }

    [Fact]
    public async Task TestUnResolvedTokenRemove()
    {
        var service = new ServiceCollection()
            .AddTransformerService(o =>
            {
                o.DefaultDefinitions = [TransformOptions.CurlyBrackets];
                o.TokenNotFoundBehavior = TokenNotFoundBehavior.Remove;
            })
            .BuildServiceProvider();

        var transformer = service.GetRequiredService<ITransformerService>();

        var rs = await transformer.TransformAsync("{A}", new Dictionary<string, string>
            (StringComparer.OrdinalIgnoreCase)
            {
                { "B", "Duy" }
            });
        rs.ShouldBeEmpty();
    }

    [Fact]
    public async Task TestUnResolvedTokenLeaveAsIs()
    {
        var service = new ServiceCollection()
            .AddTransformerService(o =>
            {
                o.DefaultDefinitions = [TransformOptions.CurlyBrackets];
                o.TokenNotFoundBehavior = TokenNotFoundBehavior.LeaveAsIs;
            })
            .BuildServiceProvider();

        var transformer = service.GetRequiredService<ITransformerService>();

        var rs = await transformer.TransformAsync("{A}", new Dictionary<string, string>
            (StringComparer.OrdinalIgnoreCase)
            {
                { "B", "Duy" }
            });
        rs.ShouldBe("{A}");
    }

    [Fact]
    public async Task TestUnResolvedTokenException()
    {
        var service = new ServiceCollection()
            .AddTransformerService(o =>
            {
                o.DefaultDefinitions = [TransformOptions.CurlyBrackets];
                o.TokenNotFoundBehavior = TokenNotFoundBehavior.ThrowError;
            })
            .BuildServiceProvider();

        var transformer = service.GetRequiredService<ITransformerService>();

        await Should.ThrowAsync<UnResolvedTokenException>(async () =>
            await transformer.TransformAsync("{A}", new Dictionary<string, string>
                (StringComparer.OrdinalIgnoreCase)
                {
                    { "B", "Duy" }
                }));
    }

    [Fact]
    public async Task DoubleCurlyBrackets_IsToken_And_ExtractToken_Tests()
    {
        var story =
            "In a quiet town nestled between rolling {{A}, there was a clockmaker named {{B}}. His shop was small, filled with the soft ticking of {{C}} timepieces.";
        var def = new TokenExtractor(TransformOptions.DoubleCurlyBrackets);

        var tokens = def.Extract(story).ToList();
        tokens.Count.ShouldBe(2);

        tokens = (await def.ExtractAsync(story)).ToList();
        tokens.Count.ShouldBe(2);
    }

    [Fact]
    public void FillTemplate_WithValidTokens_ReplacesTokensCorrectly()
    {
        // Arrange
        var template = "Hello @(name), welcome to @(location)!";
        var model = new Dictionary<string, string>
        {
            { "name", "John Doe" },
            { "location", "DKNet" }
        };

        var options = Options.Create(new TransformOptions
            { DefaultDefinitions = [new TokenDefinition("@(", ")")] });

        // Act
        var service = new TransformerService(options);

        var rs = service.Transform(template, model);

        // Assert
        Assert.Equal("Hello John Doe, welcome to DKNet!", rs);
    }
}