using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspCore.Idempotency.NpgsqlStore.Tests.Unit;

/// <summary>
///     Tests for idempotency store edge cases not covered
///     by the main repository tests, specifically for NpgsqlStore's SanitizeKey implementation.
/// </summary>
public class IdempotencyNpgsqlStoreTests
{
    #region Fields

    private readonly IDistributedCache _cache;
    private readonly ILogger<IdempotencyNpgsqlStoreTests> _logger;

    #endregion

    #region Constructors

    public IdempotencyNpgsqlStoreTests()
    {
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<IdempotencyNpgsqlStoreTests>();
    }

    #endregion

    #region Methods

    private IIdempotencyKeyStore CreateDistributedCacheStore(IdempotencyOptions? options = null) =>
        new IdempotencyDistributedCacheStore(_cache, Options.Create(options ?? new IdempotencyOptions()), _logger);

    private static IdempotentKeyInfo MakeKey(string key) =>
        new() { IdempotentKey = key, Endpoint = "/api/test", Method = "POST" };

    [Fact]
    public async Task DistributedCache_SanitizeKey_ExactCollisionFromFinding_ProducesDifferentResults()
    {
        // Arrange
        const string keyA = "POST:/ab:cd";
        const string keyB = "POST:/a:bcd";
        var store = CreateDistributedCacheStore();

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
    public async Task DistributedCache_SanitizeKey_StructurallySimilarKeys_AreAllPairwiseDistinct()
    {
        // Arrange
        string[] keys = ["GET:/a/b:x", "GET:/a:b/x", "GET:/ab:x"];
        var store = CreateDistributedCacheStore();
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
    public async Task DistributedCache_SanitizeKey_SameInputTwice_IsDeterministic()
    {
        // Arrange
        const string key = "GET:/api/orders:idem-key-123";
        var store = CreateDistributedCacheStore();
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DistributedCache_SanitizeKey_NullOrWhiteSpaceKey_ThrowsArgumentException(string? key)
    {
        // Arrange
        var store = CreateDistributedCacheStore();
        var response = new CachedResponse
        {
            StatusCode = 200,
            Body = "{}",
            ContentType = "application/json",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        // Act & Assert
        // Test the SanitizeKey method directly since that's where the validation happens
        var ex = Should.Throw<System.Reflection.TargetInvocationException>(() => 
        {
            // Access the private SanitizeKey method through reflection
            var method = typeof(IdempotencyDistributedCacheStore).GetMethod("SanitizeKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var field = typeof(IdempotencyDistributedCacheStore).GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var options = (IdempotencyOptions)field!.GetValue(store)!;
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("test");
            var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            var directStore = new IdempotencyDistributedCacheStore(cache, Options.Create(options), logger);
            method!.Invoke(directStore, [key]);
        });
        
        // The TargetInvocationException wraps the actual ArgumentException
        ex.InnerException.ShouldNotBeNull();
        ex.InnerException.ShouldBeOfType<ArgumentException>();
        ex.InnerException.Message.ShouldContain("Idempotency key cannot be null or empty");
    }

    #endregion
}