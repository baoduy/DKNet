namespace EfCore.Extensions.Tests;

#pragma warning disable CA2012 // Use ValueTasks correctly

public class UnsupportedProviderTests
{
    #region Methods

    [Fact]
    public async Task NextSeqValue_WithSqliteProvider_ShouldThrowNotSupportedException()
    {
        // Arrange
        var dbPath = Path.Combine(Path.GetTempPath(), $"UnsupportedSeq_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder()
                .UseSqlite($"Data Source={dbPath}")
                .UseAutoConfigModel([typeof(TestSequenceTypes).Assembly])
                .Options;

            await using var context = new DbContext(options);
            await context.Database.EnsureCreatedAsync();

            // Act & Assert
            await Should.ThrowAsync<NotSupportedException>(async () =>
                await context.NextSeqValue(TestSequenceTypes.TestSequence1));
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task NextSeqValue_WithSqliteProvider_ShouldContainProviderNameInMessage()
    {
        // Arrange
        var dbPath = Path.Combine(Path.GetTempPath(), $"UnsupportedMsg_{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder()
                .UseSqlite($"Data Source={dbPath}")
                .UseAutoConfigModel([typeof(TestSequenceTypes).Assembly])
                .Options;

            await using var context = new DbContext(options);
            await context.Database.EnsureCreatedAsync();

            // Act
            var ex = await Should.ThrowAsync<NotSupportedException>(async () =>
                await context.NextSeqValue(TestSequenceTypes.TestSequence1));

// Assert
        ex.Message.ShouldContain(context.Database.ProviderName!);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task IsNpgsql_WithSqliteProvider_ShouldReturnFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseSqlite("Data Source=:memory:")
            .UseAutoConfigModel([typeof(TestSequenceTypes).Assembly])
            .Options;

        await using var context = new DbContext(options);

        // Act
        var result = context.IsNpgsql();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsSqlServer_WithSqliteProvider_ShouldReturnFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseSqlite("Data Source=:memory:")
            .UseAutoConfigModel([typeof(TestSequenceTypes).Assembly])
            .Options;

        await using var context = new DbContext(options);

        // Act
        var result = context.IsSqlServer();

        // Assert
        result.ShouldBeFalse();
    }

    #endregion
}