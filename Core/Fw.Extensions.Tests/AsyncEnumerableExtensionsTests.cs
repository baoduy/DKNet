namespace Fw.Extensions.Tests;

[TestClass]
public class AsyncEnumerableExtensionsTests
{
    [TestMethod]
    public async Task ToListAsync_WithItems_ReturnsCorrectList()
    {
        // Arrange
        var items = new[] { 1, 2, 3, 4, 5 };
        var asyncEnumerable = CreateAsyncEnumerable(items);

        // Act
        var result = await asyncEnumerable.ToListAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.Count);
        CollectionAssert.AreEqual(items, result.ToArray());
    }

    [TestMethod]
    public async Task ToListAsync_WithEmptySequence_ReturnsEmptyList()
    {
        // Arrange
        var asyncEnumerable = CreateAsyncEnumerable(Array.Empty<int>());

        // Act
        var result = await asyncEnumerable.ToListAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public async Task ToListAsync_WithSingleItem_ReturnsListWithOneItem()
    {
        // Arrange
        var items = new[] { 42 };
        var asyncEnumerable = CreateAsyncEnumerable(items);

        // Act
        var result = await asyncEnumerable.ToListAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(42, result[0]);
    }

    [TestMethod]
    public async Task ToListAsync_WithStringItems_ReturnsCorrectList()
    {
        // Arrange
        var items = new[] { "hello", "world", "test" };
        var asyncEnumerable = CreateAsyncEnumerable(items);

        // Act
        var result = await asyncEnumerable.ToListAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        CollectionAssert.AreEqual(items, result.ToArray());
    }

#nullable enable
    [TestMethod]
    public async Task ToListAsync_WithNullItems_HandlesNullCorrectly()
    {
        // Arrange
        var items = new string?[] { "hello", null, "world" };
        var asyncEnumerable = CreateAsyncEnumerable(items);

        // Act
        var result = await asyncEnumerable.ToListAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("hello", result[0]);
        Assert.IsNull(result[1]);
        Assert.AreEqual("world", result[2]);
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