using DKNet.AspCore.Idempotency;

namespace AspCore.Idempotency.Tests.Unit;

public class CachedResponseTests
{
    #region Methods

    [Fact]
    public void IsExpired_WhenExpiresAtIsInFuture_ReturnsFalse()
    {
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "{}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        response.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsInPast_ReturnsTrue()
    {
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "{}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1)
        };
        response.IsExpired.ShouldBeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsNull_ReturnsFalse()
    {
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "{}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = null
        };
        response.IsExpired.ShouldBeFalse();
    }

    [Fact]
    public void CachedResponse_CanHaveNullBody()
    {
        var response = new CachedResponse
        {
            StatusCode = 204,
            Body = null,
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        response.Body.ShouldBeNull();
        response.StatusCode.ShouldBe(204);
    }

    #endregion
}
