using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SlimBus.Api.Configs.RateLimits;

namespace SlimBus.App.Tests.Unit;

public class RateLimitTests
{
    [Fact]
    public void RateLimitOptionsDefaultValuesShouldBeCorrect()
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
    public void RateLimitPolicyProviderGetPartitionKeyWithoutAuthHeaderShouldUseClientIp()
    {
        // Arrange
        using var sv = new ServiceCollection()
            .AddLogging()
            .AddSingleton<RateLimitPolicyProvider>()
            .BuildServiceProvider();
        // Arrange
        var options = Options.Create(new RateLimitOptions());
        var provider = sv.GetRequiredService<RateLimitPolicyProvider>();

        var context = new DefaultHttpContext
        {
            Connection =
            {
                RemoteIpAddress = IPAddress.Parse("192.168.1.1")
            }
        };

        // Act
        var partitionKey = provider.GetPartitionKey(context);

        // Assert
        partitionKey.ShouldStartWith("ip:");
        partitionKey.ShouldContain("192.168.1.1");
    }

    [Fact]
    public void RateLimitPolicyProviderGetPartitionKeyWithInvalidJwtTokenShouldFallbackToIp()
    {
        // Arrange
        using var sv = new ServiceCollection()
            .AddLogging()
            .AddSingleton<RateLimitPolicyProvider>()
            .BuildServiceProvider();
        // Arrange
        var options = Options.Create(new RateLimitOptions());
        var provider = sv.GetRequiredService<RateLimitPolicyProvider>();

        var context = new DefaultHttpContext
        {
            Connection =
            {
                RemoteIpAddress = IPAddress.Parse("192.168.1.1")
            }
        };
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
    public void RateLimitPolicyProviderGetPartitionKeyWithDifferentIpsShouldReturnCorrectKey(string ipAddress,
        string expectedKey)
    {
        using var sv = new ServiceCollection()
            .AddLogging()
            .AddSingleton<RateLimitPolicyProvider>()
            .BuildServiceProvider();
        // Arrange
        var options = Options.Create(new RateLimitOptions());
        var provider = sv.GetRequiredService<RateLimitPolicyProvider>();

        var context = new DefaultHttpContext
        {
            Connection =
            {
                RemoteIpAddress = IPAddress.Parse(ipAddress)
            }
        };

        // Act
        var partitionKey = provider.GetPartitionKey(context);

        // Assert
        partitionKey.ShouldBe(expectedKey);
    }

    [Fact]
    public void RateLimitPolicyProviderGetPartitionKeyWithForwardedForShouldUseForwardedIp()
    {
        // Arrange
        using var sv = new ServiceCollection()
            .AddLogging()
            .AddSingleton<RateLimitPolicyProvider>()
            .BuildServiceProvider();
        // Arrange
        var options = Options.Create(new RateLimitOptions());
        var provider = sv.GetRequiredService<RateLimitPolicyProvider>();

        var context = new DefaultHttpContext
        {
            Connection =
            {
                RemoteIpAddress = IPAddress.Parse("192.168.1.1")
            }
        };
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.1, 192.168.1.1";

        // Act
        var partitionKey = provider.GetPartitionKey(context);

        // Assert
        partitionKey.ShouldBe("ip:203.0.113.1");
    }

    [Fact]
    public void RateLimitPolicyProviderGetPartitionKeyWithNoRemoteIpShouldReturnUnknown()
    {
        // Arrange
        using var sv = new ServiceCollection()
            .AddLogging()
            .AddSingleton<RateLimitPolicyProvider>()
            .BuildServiceProvider();
        // Arrange
        var options = Options.Create(new RateLimitOptions());
        var provider = sv.GetRequiredService<RateLimitPolicyProvider>();

        var context = new DefaultHttpContext();
        // No remote IP set

        // Act
        var partitionKey = provider.GetPartitionKey(context);

        // Assert
        partitionKey.ShouldBe("ip:unknown");
    }
}