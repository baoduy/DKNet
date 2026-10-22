using DKNet.Fw.Extensions.Primitives;

namespace Fw.Extensions.Tests.Primitives;

/// <summary>
///     The DRK-2362 §6a D5 rows of <see cref="StringExtensions.SanitizeForLogging" /> beyond the frozen outline in
///     <see cref="StringExtensionsTests" />.
/// </summary>
public class SanitizeForLoggingTests
{
    #region Methods

    [Theory]
    [InlineData("payment\0-42", "payment-42")]
    [InlineData("payment\u0085-42", "payment-42")]
    [InlineData("pay\rment\n-\t42", "payment-42")]
    [InlineData(" \r\n payment 42 \t ", "payment 42")]
    [InlineData("", "")]
    public void SanitizeForLogging_InputDomain_RemovesControlCharactersAndTrims(string input, string output) =>
        input.SanitizeForLogging().ShouldBe(output);

    [Fact]
    public void SanitizeForLogging_Null_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((string)null!).SanitizeForLogging()).ParamName.ShouldBe("value");

    #endregion
}
