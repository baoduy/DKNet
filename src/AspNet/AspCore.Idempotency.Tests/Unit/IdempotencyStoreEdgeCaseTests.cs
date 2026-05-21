using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AspCore.Idempotency.Tests.Unit;

/// <summary>
///     Tests for <see cref="IdempotencyDistributedCacheStore" /> edge cases not covered
///     by the main repository tests.
/// </summary>
public class IdempotencyStoreEdgeCaseTests
{
    #region Fields

    private readonly IDistributedCache _cache;
    private readonly ILogger<IdempotencyEndpointFilter> _logger;

    #endregion

    #region Constructors

    public IdempotencyStoreEdgeCaseTests()
    {
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<IdempotencyEndpointFilter>();
    }

    #endregion

    #region Methods

    private IdempotencyDistributedCacheStore CreateStore(IdempotencyOptions? options = null) =>
        new(_cache, Options.Create(options ?? new IdempotencyOptions()), _logger);

    private static IdempotentKeyInfo MakeKey(string key) =>
        new() { IdempotentKey = key, Endpoint = "/api/test", Method = "POST" };

    [Fact]
    public async Task IsKeyProcessedAsync_WhenResponseIsAlreadyExpired_ReturnsFalseAndRemovesFromCache()
    {
        // Arrange: Store an already-expired response
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "{\"id\": 1}",
            ContentType = "application/json",
            CreatedAt = now.AddHours(-2),
            ExpiresAt = now.AddSeconds(-1) // already expired
        };

        var key = Guid.NewGuid().ToString();
        await store.MarkKeyAsProcessedAsync(MakeKey(key), response);

        // Act
        var result = await store.IsKeyProcessedAsync(MakeKey(key));

        // Assert
        result.processed.ShouldBeFalse();
        result.response.ShouldBeNull();
    }

    [Fact]
    public async Task IsKeyProcessedAsync_WhenResponseHasNoExpiration_NeverExpires()
    {
        // Arrange: Store a response with null ExpiresAt
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "{\"id\": 1}",
            ContentType = "application/json",
            CreatedAt = now,
            ExpiresAt = null
        };

        var key = Guid.NewGuid().ToString();
        await store.MarkKeyAsProcessedAsync(MakeKey(key), response);

        // Act
        var result = await store.IsKeyProcessedAsync(MakeKey(key));

        // Assert
        result.processed.ShouldBeTrue();
        result.response.ShouldNotBeNull();
        result.response!.ExpiresAt.ShouldBeNull();
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_DifferentEndpoints_StoredSeparately()
    {
        // Arrange: Same idempotency key but different endpoints
        var store = CreateStore();
        var idempotencyKey = Guid.NewGuid().ToString();

        var keyInfo1 = new IdempotentKeyInfo { IdempotentKey = idempotencyKey, Endpoint = "/api/orders", Method = "POST" };
        var keyInfo2 = new IdempotentKeyInfo { IdempotentKey = idempotencyKey, Endpoint = "/api/items", Method = "POST" };

        var response1 = new CachedResponse
        {
            StatusCode = 201, Body = "{\"type\":\"order\"}", ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        var response2 = new CachedResponse
        {
            StatusCode = 201, Body = "{\"type\":\"item\"}", ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act
        await store.MarkKeyAsProcessedAsync(keyInfo1, response1);
        await store.MarkKeyAsProcessedAsync(keyInfo2, response2);

        var result1 = await store.IsKeyProcessedAsync(keyInfo1);
        var result2 = await store.IsKeyProcessedAsync(keyInfo2);

        // Assert: Both keys stored independently
        result1.processed.ShouldBeTrue();
        result2.processed.ShouldBeTrue();
        result1.response!.Body.ShouldBe("{\"type\":\"order\"}");
        result2.response!.Body.ShouldBe("{\"type\":\"item\"}");
    }

    [Fact]
    public async Task MarkKeyAsProcessedAsync_DifferentMethods_StoredSeparately()
    {
        // Arrange: Same endpoint and key but different HTTP methods
        var store = CreateStore();
        var idempotencyKey = Guid.NewGuid().ToString();

        var postKey = new IdempotentKeyInfo { IdempotentKey = idempotencyKey, Endpoint = "/api/orders", Method = "POST" };
        var putKey = new IdempotentKeyInfo { IdempotentKey = idempotencyKey, Endpoint = "/api/orders", Method = "PUT" };

        var response = new CachedResponse
        {
            StatusCode = 200, Body = "{}", ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act
        await store.MarkKeyAsProcessedAsync(postKey, response);

        var postResult = await store.IsKeyProcessedAsync(postKey);
        var putResult = await store.IsKeyProcessedAsync(putKey);

        // Assert: POST key found, PUT key not found
        postResult.processed.ShouldBeTrue();
        putResult.processed.ShouldBeFalse();
    }

    #endregion
}
