namespace EfCore.Extensions.Tests;

#pragma warning disable CA2012 // Use ValueTasks correctly

public class NpgsqlSequenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public void DatabaseProvider_ShouldBeNpgsql()
    {
        // Act
        var providerName = _db.Database.ProviderName;

        // Assert
        providerName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public void IsNpgsql_WithPostgresProvider_ShouldReturnTrue()
    {
        // Act
        var result = _db.IsNpgsql();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsSqlServer_WithPostgresProvider_ShouldReturnFalse()
    {
        // Act
        var result = _db.IsSqlServer();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task NextSeqValue_WithValidSequence_ShouldReturnValue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(fixture.GetConnectionString("NpgsqlSequenceDb"))
            .UseAutoConfigModel([typeof(DbContext).Assembly])
            .Options;

        await using var context = new DbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Manual create still needed: this scans typeof(DbContext).Assembly (EF Core's own assembly), which
        // never contains TestSequenceTypes, so nothing gets auto-registered regardless of provider. Contrast
        // with NextSeqValueWithFormat_ShouldReturnFormattedValue below, which scans the right assembly and
        // needs no manual create now that Npgsql registration is enabled.
        await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS seq");
        await context.Database.ExecuteSqlRawAsync("CREATE SEQUENCE IF NOT EXISTS seq.\"Seq_TestSequence1\" START WITH 100 INCREMENT BY 5");

        // Act
        var value = await context.NextSeqValue(TestSequenceTypes.TestSequence1);

        // Assert
        value.ShouldNotBeNull();
        // Postgres nextval() always returns bigint (long)
        value.ShouldBeAssignableTo<long>();
        ((long)value).ShouldBeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public async Task NextSeqValueWithFormat_ShouldReturnFormattedValue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(fixture.GetConnectionString("NpgsqlFormatDb"))
            .UseAutoConfigModel([typeof(TestSequenceTypes).Assembly])
            .Options;

        await using var context = new DbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Manual create still required: EF's default ModelCacheKey is keyed on DbContext CLR type only, not
        // on the assemblies passed to UseAutoConfigModel. Since NextSeqValue_WithValidSequence_ShouldReturnValue
        // above also uses the plain DbContext type (with a different assemblies list), running the suite
        // together can serve this test a stale cached model with no sequences - relying on auto-creation here
        // would be order-dependent. This is a pre-existing model-caching characteristic, out of scope for this fix.
        await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS seq");
        await context.Database.ExecuteSqlRawAsync("CREATE SEQUENCE IF NOT EXISTS seq.\"Seq_TestSequence1\" START WITH 100 INCREMENT BY 5");

        // Act
        var formattedValue = await context.NextSeqValueWithFormat(TestSequenceTypes.TestSequence1);

        // Assert
        formattedValue.ShouldNotBeNullOrEmpty();
        formattedValue.ShouldStartWith("TEST-");
    }

    [Fact]
    public async Task NextSeqValue_WithSequencesTestEnum_ShouldReturnValue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(fixture.GetConnectionString("NpgsqlSequencesDb"))
            .UseAutoConfigModel([typeof(MyDbContext).Assembly])
            .Options;

        await using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // Seq_Invoice/Seq_Payment are now auto-created by EnsureCreatedAsync (annotated with [Sequence]).
        // Seq_Order still needs a manual create: Order has no [Sequence] attribute, so the registration-loop
        // fix (skip non-annotated members) deliberately excludes it from the model on every provider.
        await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS seq");
        await context.Database.ExecuteSqlRawAsync("CREATE SEQUENCE IF NOT EXISTS seq.\"Seq_Order\" START WITH 1 INCREMENT BY 1");

        // Act
        // Postgres nextval() returns bigint, so use long
        var val1 = await context.NextSeqValue<SequencesTest, long>(SequencesTest.Invoice);
        var val2 = await context.NextSeqValue<SequencesTest, long>(SequencesTest.Order);

        // Assert
        val1!.Value.ShouldBeGreaterThan(0L);
        val2!.Value.ShouldBeGreaterThan(0L);
    }

    [Fact]
    public async Task NextSeqValueWithFormat_WithSequencesTestEnum_ShouldReturnFormattedValue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(fixture.GetConnectionString("NpgsqlFormatSeqDb"))
            .UseAutoConfigModel([typeof(MyDbContext).Assembly])
            .Options;

        await using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        // No manual create needed: Invoice carries [Sequence], so EnsureCreatedAsync auto-creates
        // Seq_Invoice on Npgsql now that registration is enabled for this provider.

        // Act
        var val = await context.NextSeqValueWithFormat(SequencesTest.Invoice);

        // Assert
        // NextSeqValueWithFormat uses DateTime.UtcNow internally, format: {0:yyMMdd}{1:00000}
        val.ShouldStartWith(string.Format(CultureInfo.CurrentCulture, "T{0:yyMMdd}", DateTime.UtcNow));
    }

    #endregion
}