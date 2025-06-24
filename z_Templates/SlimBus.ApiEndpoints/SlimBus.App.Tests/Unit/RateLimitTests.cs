using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SlimBus.Api.Configs.RateLimits;

namespace SlimBus.App.Tests.Unit;

public class RateLimitTests
{
    [Fact]
    public void RateLimitOptions_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var options = new RateLimitOptions();

        // Assert
        options.DefaultRequestLimit.ShouldBe(2);
        options.TimeWindowInSeconds.ShouldBe(1);
        options.QueueLimit.ShouldBe(0);
        options.QueueProcessingOrder.ShouldBe(RateLimitQueueProcessingOrder.OldestFirst);
    }

    [Fact]
    public void RateLimitPolicyProvider_GetPartitionKey_WithoutAuthHeader_ShouldUseClientIp()
    {
        // Arrange
        var options = Options.Create(new RateLimitOptions());
        var logger = Substitute.For<ILogger<RateLimitPolicyProvider>>();
        var provider = new RateLimitPolicyProvider(options, logger);
        
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        // Act
        var partitionKey = provider.GetPartitionKey(context);

        // Assert
        partitionKey.ShouldStartWith("ip:");
        partitionKey.ShouldContain("192.168.1.1");
    }

    [Fact]
    public void RateLimitPolicyProvider_GetPartitionKey_WithInvalidJwtToken_ShouldFallbackToIp()
    {
        // Arrange
        var options = Options.Create(new RateLimitOptions());
        var logger = Substitute.For<ILogger<RateLimitPolicyProvider>>();
        var provider = new RateLimitPolicyProvider(options, logger);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        context.Request.Headers.Authorization = "Bearer invalid-token";

        // Act
        var partitionKey = provider.GetPartitionKey(context);

        // Assert
        partitionKey.ShouldStartWith("ip:");
        partitionKey.ShouldContain("192.168.1.1");
    }

    [Theory]
    [InlineData("192.168.1.1", "ip:192.168.1.1")]
    [InlineData("127.0.0.1", "ip:127.0.0.1")]
    [InlineData("10.0.0.1", "ip:10.0.0.1")]
    public void RateLimitPolicyProvider_GetPartitionKey_WithDifferentIps_ShouldReturnCorrectKey(string ipAddress, string expectedKey)
    {
        // Arrange
        var options = Options.Create(new RateLimitOptions());
        var logger = Substitute.For<ILogger<RateLimitPolicyProvider>>();
        var provider = new RateLimitPolicyProvider(options, logger);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);

        // Act
        var partitionKey = provider.GetPartitionKey(context);

        // Assert
        partitionKey.ShouldBe(expectedKey);
    }

    [Fact]
    public void RateLimitPolicyProvider_GetPartitionKey_WithForwardedFor_ShouldUseForwardedIp()
    {
        // Arrange
        var options = Options.Create(new RateLimitOptions());
        var logger = Substitute.For<ILogger<RateLimitPolicyProvider>>();
        var provider = new RateLimitPolicyProvider(options, logger);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.1, 192.168.1.1";

        // Act
        var partitionKey = provider.GetPartitionKey(context);

        // Assert
        partitionKey.ShouldBe("ip:203.0.113.1");
    }

    [Fact]
    public void RateLimitPolicyProvider_GetPartitionKey_WithNoRemoteIp_ShouldReturnUnknown()
    {
        // Arrange
        var options = Options.Create(new RateLimitOptions());
        var logger = Substitute.For<ILogger<RateLimitPolicyProvider>>();
        var provider = new RateLimitPolicyProvider(options, logger);

        var context = new DefaultHttpContext();
        // No remote IP set

        // Act
        var partitionKey = provider.GetPartitionKey(context);

        // Assert
        partitionKey.ShouldBe("ip:unknown");
    }
}