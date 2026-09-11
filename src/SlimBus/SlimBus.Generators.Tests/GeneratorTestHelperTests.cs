using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

/// <summary>
/// Covers <see cref="GeneratorTestHelper.NormaliseNewLines"/> itself, not generator behaviour — asserting
/// against a literal that always carries <c>\r\n</c> is what makes this guard load-bearing on
/// <c>ubuntu-latest</c> CI too: asserting on a live generator run would be vacuous there, since
/// <c>Environment.NewLine</c> is already <c>\n</c> on Linux.
/// </summary>
public class GeneratorTestHelperTests
{
    [Theory]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\nb", "a\nb")]
    [InlineData("", "")]
    [InlineData("\r\na\r\nb\r\n", "\na\nb\n")]
    [InlineData("a\r\n\r\nb", "a\n\nb")]
    [InlineData("a\rb", "a\rb")]
    public void NormaliseNewLines_GivenInput_ReturnsTextWithOnlyLineFeeds(string input, string expected)
    {
        var result = GeneratorTestHelper.NormaliseNewLines(input);

        result.ShouldBe(expected);
    }
}
