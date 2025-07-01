namespace Fw.Extensions.Tests;


public class AsyncEnumerableExtensionsTests
{
    [Fact]
    public async Task ToListAsync_WithItems_ReturnsCorrectList()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5 };
        var asyncEnumerable = CreateAsyncEnumerable(items);

        // Act
        var result = await asyncEnumerable.ToListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        CollectionAssert.AreEqual(items, result.ToArray());
    }

    [Fact]
    public async Task ToListAsync_WithEmptySequence_ReturnsEmptyList()
    {
        // Arrange
        var asyncEnumerable = CreateAsyncEnumerable(Array.Empty<int>());

        // Act
        var result = await asyncEnumerable.ToListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task ToListAsync_WithSingleItem_ReturnsListWithOneItem()
    {
        // Arrange
        var items = new[] { 42 };
        var asyncEnumerable = CreateAsyncEnumerable(items);

        // Act
        var result = await asyncEnumerable.ToListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].ShouldBe(42);
    }

    [Fact]
    public async Task ToListAsync_WithStringItems_ReturnsCorrectList()
    {
        // Arrange
        var items = new[] { "hello", "world", "test" };
        var asyncEnumerable = CreateAsyncEnumerable(items);

        // Act
        var result = await asyncEnumerable.ToListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        CollectionAssert.AreEqual(items, result.ToArray());
    }

#nullable enable
    [Fact]
    public async Task ToListAsync_WithNullItems_HandlesNullCorrectly()
    {
        // Arrange
        var items = new string?[] { "hello", null, "world" };
        var asyncEnumerable = CreateAsyncEnumerable(items);

        // Act
        var result = await asyncEnumerable.ToListAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result[0].ShouldBe("hello");
        result[1].ShouldBeNull();
        result[2].ShouldBe("world");
    }
#nullable disable

    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Delay(1); // Simulate async behavior
            yield return item;
        }
    }
}