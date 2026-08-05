using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspCore.Idempotency.Tests.Unit;

/// <summary>
///     Tests for <see cref="IdempotencyDistributedCacheStore" /> edge cases not covered
///     by the main repository tests, specifically for collision detection and deterministic behavior.
/// </summary>
public class IdempotencyDistributedCacheStoreTests
{
    #region Fields

    private readonly IDistributedCache _cache;
    private readonly ILogger<IdempotencyEndpointFilter> _logger;

    #endregion

    #region Constructors

    public IdempotencyDistributedCacheStoreTests()
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
    public async Task SanitizeKey_ExactCollisionFromFinding_ProducesDifferentResults()
    {
        // Arrange
        const string keyA = "POST:/ab:cd";
        const string keyB = "POST:/a:bcd";
        var store = CreateStore();

        // Act
        var responseA = new CachedResponse
        {
            StatusCode = 200,
            Body = "{\"key\": \"A\"}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        
        var responseB = new CachedResponse
        {
            StatusCode = 200,
            Body = "{\"key\": \"B\"}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        await store.MarkKeyAsProcessedAsync(MakeKey(keyA), responseA);
        await store.MarkKeyAsProcessedAsync(MakeKey(keyB), responseB);

        var resultA = await store.IsKeyProcessedAsync(MakeKey(keyA));
        var resultB = await store.IsKeyProcessedAsync(MakeKey(keyB));

        // Assert
        resultA.processed.ShouldBeTrue();
        resultB.processed.ShouldBeTrue();
        resultA.response!.Body.ShouldBe("{\"key\": \"A\"}");
        resultB.response!.Body.ShouldBe("{\"key\": \"B\"}");
    }

    [Fact]
    public async Task SanitizeKey_StructurallySimilarKeys_AreAllPairwiseDistinct()
    {
        // Arrange
        string[] keys = ["GET:/a/b:x", "GET:/a:b/x", "GET:/ab:x"];
        var store = CreateStore();
        var responses = new List<CachedResponse>();

        // Act
        for (var i = 0; i < keys.Length; i++)
        {
            var response = new CachedResponse
            {
                StatusCode = 200,
                Body = $"{{\"key\": {i}}}",
                ContentType = "application/json",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            };
            
            responses.Add(response);
            await store.MarkKeyAsProcessedAsync(MakeKey(keys[i]), response);
        }

        // Assert
        for (var i = 0; i < keys.Length; i++)
        {
            var result = await store.IsKeyProcessedAsync(MakeKey(keys[i]));
            result.processed.ShouldBeTrue();
            result.response!.Body.ShouldBe($"{{\"key\": {i}}}");
        }
    }

    [Fact]
    public async Task SanitizeKey_SameInputTwice_IsDeterministic()
    {
        // Arrange
        const string key = "GET:/api/orders:idem-key-123";
        var store = CreateStore();
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "{\"id\": 123}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act
        await store.MarkKeyAsProcessedAsync(MakeKey(key), response);
        var result1 = await store.IsKeyProcessedAsync(MakeKey(key));
        var result2 = await store.IsKeyProcessedAsync(MakeKey(key));

        // Assert
        result1.processed.ShouldBeTrue();
        result2.processed.ShouldBeTrue();
        result1.response!.Body.ShouldBe("{\"id\": 123}");
        result2.response!.Body.ShouldBe("{\"id\": 123}");
    }

    #endregion
}