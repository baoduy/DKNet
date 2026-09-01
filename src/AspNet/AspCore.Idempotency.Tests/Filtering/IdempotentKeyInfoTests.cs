using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;

namespace AspCore.Idempotency.Tests.Filtering;

public class IdempotentKeyInfoTests
{
    #region Fields

    private static IdempotencyOptions DefaultOptions => new();

    #endregion

    #region Methods

    [Fact]
    public void CompositeKey_CombinesMethodEndpointAndKey()
    {
        var info = new IdempotentKeyInfo { IdempotentKey = "test-key", Endpoint = "/api/orders", Method = "POST" };
        info.CompositeKey.ShouldBe(":POST:/api/orders:test-key");
    }

    [Fact]
    public void CompositeKey_WhenKeyIsNull_UsesEmptyString()
    {
        var info = new IdempotentKeyInfo { IdempotentKey = null, Endpoint = "/api/orders", Method = "GET" };
        info.CompositeKey.ShouldBe(":GET:/api/orders:");
    }

    [Fact]
    public void Scope_DefaultValue_IsEmptyString()
    {
        var info = new IdempotentKeyInfo { IdempotentKey = "test-key", Endpoint = "/api/orders", Method = "POST" };
        info.Scope.ShouldBe(string.Empty);
    }

    [Fact]
    public void CompositeKey_WhenScopeSet_PrependsScopeSegment()
    {
        var info = new IdempotentKeyInfo
        {
            Scope = "user:user-42",
            IdempotentKey = "test-key",
            Endpoint = "/api/orders",
            Method = "POST"
        };
        info.CompositeKey.ShouldBe("user:user-42:POST:/api/orders:test-key");
    }

    [Fact]
    public void IsValid_WhenKeyHasInvalidChars_ReturnsFailed()
    {
        var info = new IdempotentKeyInfo
            { IdempotentKey = "invalid!@#$key", Endpoint = "/api/test", Method = "POST" };
        var result = info.IsValid(DefaultOptions);
        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Message.ShouldContain("format is invalid");
    }

    [Fact]
    public void IsValid_WhenKeyIsEmpty_ReturnsFailed()
    {
        var info = new IdempotentKeyInfo { IdempotentKey = "", Endpoint = "/api/test", Method = "POST" };
        var result = info.IsValid(DefaultOptions);
        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WhenKeyIsNull_ReturnsFailed()
    {
        var info = new IdempotentKeyInfo { IdempotentKey = null, Endpoint = "/api/test", Method = "POST" };
        var result = info.IsValid(DefaultOptions);
        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WhenKeyIsTooLong_ReturnsFailed()
    {
        var options = new IdempotencyOptions { MaxIdempotencyKeyLength = 10 };
        var info = new IdempotentKeyInfo
            { IdempotentKey = "12345678901", Endpoint = "/api/test", Method = "POST" };
        var result = info.IsValid(options);
        result.IsFailed.ShouldBeTrue();
        result.Errors[0].Message.ShouldContain("must not exceed");
    }

    [Fact]
    public void IsValid_WhenKeyIsValid_ReturnsOk()
    {
        var info = new IdempotentKeyInfo
            { IdempotentKey = "valid-key-123_ABC", Endpoint = "/api/test", Method = "POST" };
        var result = info.IsValid(DefaultOptions);
        result.IsFailed.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WhenKeyIsWhitespace_ReturnsFailed()
    {
        var info = new IdempotentKeyInfo { IdempotentKey = "   ", Endpoint = "/api/test", Method = "POST" };
        var result = info.IsValid(DefaultOptions);
        result.IsFailed.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WhenKeyMatchesDefaultPattern_ReturnsOk()
    {
        var info = new IdempotentKeyInfo
        {
            IdempotentKey = Guid.NewGuid().ToString(), // UUID format: uses hyphens and alphanumeric
            Endpoint = "/api/test",
            Method = "POST"
        };
        var result = info.IsValid(DefaultOptions);
        result.IsFailed.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WhenKeyEndsWithSingleTrailingNewline_ReturnsOk()
    {
        // "$" without RegexOptions.Multiline still matches immediately before a single trailing '\n',
        // so a key ending in one line break passes the default format pattern unmodified.
        var info = new IdempotentKeyInfo { IdempotentKey = "order-42\n", Endpoint = "/api/test", Method = "POST" };
        var result = info.IsValid(DefaultOptions);
        result.IsFailed.ShouldBeFalse();
    }

    [Fact]
    public void SafeKey_WhenKeyIsNull_ReturnsEmptyString()
    {
        var info = new IdempotentKeyInfo { IdempotentKey = null, Endpoint = "/api/test", Method = "POST" };
        info.SafeKey.ShouldBe(string.Empty);
    }

    [Fact]
    public void SafeKey_WhenKeyIsEmpty_ReturnsEmptyString()
    {
        var info = new IdempotentKeyInfo { IdempotentKey = "", Endpoint = "/api/test", Method = "POST" };
        info.SafeKey.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("abc\r\ndef", "abc def")]
    [InlineData("abc\ndef", "abc def")]
    [InlineData("abc\rdef", "abc def")]
    [InlineData("abc\tdef", "abcdef")]
    [InlineData("order-42\n", "order-42 ")]
    public void SafeKey_WhenKeyContainsLineBreaksOrControlChars_NeverContainsARawLineBreak(
        string rawKey,
        string expectedSafeKey)
    {
        var info = new IdempotentKeyInfo { IdempotentKey = rawKey, Endpoint = "/api/test", Method = "POST" };

        info.SafeKey.ShouldBe(expectedSafeKey);
        info.SafeKey.ShouldNotContain('\n');
        info.SafeKey.ShouldNotContain('\r');
    }

    [Theory]
    [InlineData(0x2028)] // LINE SEPARATOR
    [InlineData(0x2029)] // PARAGRAPH SEPARATOR
    public void SafeKey_WhenKeyContainsUnicodeLineOrParagraphSeparator_RemovesIt(int codepoint)
    {
        var separator = (char)codepoint;
        var info = new IdempotentKeyInfo
            { IdempotentKey = $"abc{separator}def", Endpoint = "/api/test", Method = "POST" };

        info.SafeKey.ShouldBe("abcdef");
    }

    [Fact]
    public void CompositeKey_WhenKeyHasTrailingNewline_UsesRawKeyUnchanged()
    {
        // Storage/lookup must keep using the raw key, never the log-safe projection.
        var info = new IdempotentKeyInfo
            { IdempotentKey = "order-42\n", Endpoint = "/api/orders", Method = "POST" };
        info.CompositeKey.ShouldBe(":POST:/api/orders:order-42\n");
    }

    [Fact]
    public void ToString_NeverIncludesScopeOrRawKey()
    {
        var info = new IdempotentKeyInfo
        {
            Scope = "user:user-42",
            IdempotentKey = "order-42\n",
            Endpoint = "/api/orders",
            Method = "POST"
        };

        var text = info.ToString();

        text.ShouldBe("Key=order-42 , Method=POST, Endpoint=/api/orders");
        text.ShouldNotContain("user:");
        text.ShouldNotContain('\n');
    }

    #endregion
}
