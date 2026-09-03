using System.Linq;
using DKNet.Svc.Transformation.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Svc.Transform.Tests;

public class TransformTests
{
    #region Methods

    /// <summary>
    ///     Helper method to create TransformOptions with specific token definitions.
    /// </summary>
    private static IOptions<TransformOptions> CreateOptionsWithDefinitions(params ITokenDefinition[] definitions)
    {
        var options = new TransformOptions();
        options.DefaultDefinitions.Clear();
        foreach (var definition in definitions)
        {
            options.DefaultDefinitions.Add(definition);
        }

        return Options.Create(options);
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

        var options = CreateOptionsWithDefinitions(new TokenDefinition("@(", ")"));

        // Act
        var service = new TransformerService(options);
        var rs = service.Transform(template, model);

        // Assert
        Assert.Equal("Hello John Doe, welcome to DKNet!", rs);
    }

    [Fact]
    public void AddTransformerService_CalledTwice_RegistersServiceOnlyOnce()
    {
        var services = new ServiceCollection();

        services.AddTransformerService(o => o.TokenNotFoundBehavior = TokenNotFoundBehavior.Remove);
        services.AddTransformerService(o => o.TokenNotFoundBehavior = TokenNotFoundBehavior.ThrowError);

        services.Count(s => s.ServiceType == typeof(ITransformerService)).ShouldBe(1);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TransformOptions>>();
        options.Value.TokenNotFoundBehavior.ShouldBe(TokenNotFoundBehavior.Remove);
    }

    [Fact]
    public async Task TestUnResolvedTokenException()
    {
        var service = new ServiceCollection()
            .AddTransformerService(o =>
            {
                o.DefaultDefinitions.Clear();
                o.DefaultDefinitions.Add(TransformOptions.CurlyBrackets);
                o.TokenNotFoundBehavior = TokenNotFoundBehavior.ThrowError;
            })
            .BuildServiceProvider();

        var transformer = service.GetRequiredService<ITransformerService>();

        await Should.ThrowAsync<UnResolvedTokenException>(async () =>
            await transformer.TransformAsync(
                "{A}",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "B", "Duy" }
                }));
    }

    [Fact]
    public async Task TestUnResolvedTokenLeaveAsIs()
    {
        var service = new ServiceCollection()
            .AddTransformerService(o =>
            {
                o.DefaultDefinitions.Clear();
                o.DefaultDefinitions.Add(TransformOptions.CurlyBrackets);
                o.TokenNotFoundBehavior = TokenNotFoundBehavior.LeaveAsIs;
            })
            .BuildServiceProvider();

        var transformer = service.GetRequiredService<ITransformerService>();

        var rs = await transformer.TransformAsync(
            "{A}",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "B", "Duy" }
            });
        rs.ShouldBe("{A}");
    }

    [Fact]
    public async Task TestUnResolvedTokenRemove()
    {
        var service = new ServiceCollection()
            .AddTransformerService(o =>
            {
                o.DefaultDefinitions.Clear();
                o.DefaultDefinitions.Add(TransformOptions.CurlyBrackets);
                o.TokenNotFoundBehavior = TokenNotFoundBehavior.Remove;
            })
            .BuildServiceProvider();

        var transformer = service.GetRequiredService<ITransformerService>();

        var rs = await transformer.TransformAsync(
            "{A}",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "B", "Duy" }
            });
        rs.ShouldBeEmpty();
    }

    [Fact]
    public void Transform_CalledTwiceOnSameInstance_WithDifferentAdditionalData_DoesNotReuseStaleCache()
    {
        // Arrange - a single TransformerService instance is reused across two calls, as would happen
        // if a consumer captured it in a scoped/singleton class instead of taking a fresh transient.
        var options = Options.Create(new TransformOptions());
        var service = new TransformerService(options);

        // Act
        var first = service.Transform("Hello [Name]", new { Name = "Alice" });
        var second = service.Transform("Hello [Name]", new { Name = "Bob" });

        // Assert
        first.ShouldBe("Hello Alice");
        second.ShouldBe("Hello Bob");
    }

    [Fact]
    public async Task TransformAsync_CalledTwiceOnSameInstance_WithDifferentAdditionalData_DoesNotReuseStaleCache()
    {
        var options = Options.Create(new TransformOptions());
        var service = new TransformerService(options);

        var first = await service.TransformAsync("Hello [Name]", new { Name = "Alice" });
        var second = await service.TransformAsync("Hello [Name]", new { Name = "Bob" });

        first.ShouldBe("Hello Alice");
        second.ShouldBe("Hello Bob");
    }

    [Fact]
    public void Transform_TokenRepeatedInTemplate_ResolvesValueOnlyOnce()
    {
        // Arrange - a spy that counts how many times its property is read via reflection proves
        // the per-call cache still deduplicates repeated tokens within one template.
        var spy = new CountingNameProvider();
        var options = Options.Create(new TransformOptions());
        var service = new TransformerService(options);

        // Act
        var result = service.Transform("[Name] and [Name] and [Name]", spy);

        // Assert
        result.ShouldBe("Duy and Duy and Duy");
        spy.ReadCount.ShouldBe(1);
    }

    private sealed class CountingNameProvider
    {
        public int ReadCount { get; private set; }

        public string Name
        {
            get
            {
                ReadCount++;
                return "Duy";
            }
        }
    }

    [Fact]
    public void Transform_OptionsDefinitionsMutatedAfterConstruction_UsesExtractorsCapturedAtConstruction()
    {
        // Arrange: the service only knows about square brackets at construction time.
        var options = CreateOptionsWithDefinitions(TransformOptions.SquareBrackets);
        var service = new TransformerService(options);

        // Mutate the options after construction - if extractors were rebuilt on every call
        // (the bug P30 describes), this curly-bracket definition would now be picked up too.
        options.Value.DefaultDefinitions.Add(TransformOptions.CurlyBrackets);

        // Act: a template containing only a curly-bracket token, which is not a token
        // under the extractors captured at construction, so it should pass through untouched
        // instead of being extracted and failing to resolve (TokenNotFoundBehavior.ThrowError).
        var rs = service.Transform("Hello {Name}");

        // Assert
        rs.ShouldBe("Hello {Name}");
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
    public async Task TransformHugeTemplateAsyncDisableCacheTest()
    {
        var d = Path.GetDirectoryName(typeof(TransformTests).Assembly.Location);
        var template = await File.ReadAllTextAsync(d + "/TestData/Data.txt");

        var options = CreateOptionsWithDefinitions(
            TransformOptions.AngledBrackets,
            TransformOptions.CurlyBrackets,
            TransformOptions.SquareBrackets);

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

        var options = CreateOptionsWithDefinitions(
            TransformOptions.AngledBrackets,
            TransformOptions.CurlyBrackets,
            TransformOptions.SquareBrackets);

        var t = new TransformerService(options);
        var s = await t.TransformAsync(template, new { A = "Hoang", B = "Bao", C = "Duy", D = "DKNet" });

        s.ShouldContain("Bao");
        s.ShouldNotContain("{");
        s.ShouldNotContain("[");
        s.ShouldNotContain("<");
    }

    #endregion
}