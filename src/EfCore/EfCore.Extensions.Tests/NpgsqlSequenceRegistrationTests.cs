namespace EfCore.Extensions.Tests;

#pragma warning disable CA2012 // Use ValueTasks correctly

/// <summary>
///     Regression tests for sequence registration on Npgsql.
///     Verifies annotated members get sequences and unannotated members (Seq_None) do not.
/// </summary>
public class NpgsqlSequenceRegistrationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public void Model_ShouldContainSequenceForAnnotatedInvoice()
    {
        var seq = _db.Model.FindSequence("Seq_Invoice", "seq");
        seq.ShouldNotBeNull();
    }

    [Fact]
    public void Model_ShouldContainSequenceForAnnotatedPayment()
    {
        var seq = _db.Model.FindSequence("Seq_Payment", "seq");
        seq.ShouldNotBeNull();
    }

    [Fact]
    public void Model_ShouldContainSequenceForAnnotatedTestSequence1()
    {
        var seq = _db.Model.FindSequence("Seq_TestSequence1", "seq");
        seq.ShouldNotBeNull();
    }

    [Fact]
    public void Model_ShouldNotContainSequenceForUnannotatedOrder()
    {
        // Order has no [Sequence] attribute — regression test for Seq_None removal.
        // Previously, ALL enum members would be treated as having a default SequenceAttribute;
        // now only members with an explicit [Sequence] attribute get a sequence.
        var seq = _db.Model.FindSequence("Seq_Order", "seq");
        seq.ShouldBeNull();
    }

    [Fact]
    public async Task Sequences_ShouldExistInDatabaseForAnnotatedMembers()
    {
        // Verify physical sequences exist in Postgres for annotated members.
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(fixture.GetConnectionString("NpgsqlRegDb"))
            .UseAutoConfigModel([typeof(MyDbContext).Assembly])
            .Options;

        await using var context = new MyDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var names = await context.Database
            .SqlQueryRaw<string>(
                "SELECT sequencename FROM pg_catalog.pg_sequences WHERE sequencename LIKE 'Seq_%'")
            .ToListAsync();

        names.ShouldContain("Seq_Invoice");
        names.ShouldContain("Seq_Payment");
        names.ShouldNotContain("Seq_Order");
    }

    [Fact]
    public void AutoConfigModelCustomizer_ShouldNotRegisterSequencesForUnsupportedProvider()
    {
        // The gate in AutoConfigModelCustomizer is dbContext.IsSqlServer() || dbContext.IsNpgsql().
        // Sqlite should NOT trigger sequence registration.
        var options = new DbContextOptionsBuilder()
            .UseSqlite("Data Source=:memory:")
            .UseAutoConfigModel([typeof(TestSequenceTypes).Assembly])
            .Options;

        using var context = new DbContext(options);
        context.Database.EnsureCreated();

        var sequences = context.Model.GetSequences().ToList();
        sequences.ShouldBeEmpty();
    }

    [Fact]
    public async Task RegisterSequencesFromEnumType_ShouldSkipUnannotatedFields()
    {
        // Direct test of the RegisterSequences logic: create a fresh Npgsql context,
        // verify the generated DDL contains CREATE SEQUENCE for annotated members
        // and does NOT contain it for unannotated Order.
        var options = new DbContextOptionsBuilder()
            .UseNpgsql(fixture.GetConnectionString("NpgsqlScriptDb"))
            .UseAutoConfigModel([typeof(MyDbContext).Assembly])
            .Options;

        await using var context = new MyDbContext(options);
        var script = context.Database.GenerateCreateScript();

        script.ShouldContain("CREATE SEQUENCE", Case.Insensitive);
        script.ShouldContain("\"Seq_Invoice\"");
        script.ShouldContain("\"Seq_Payment\"");
        script.ShouldNotContain("\"Seq_Order\"");
    }

    #endregion
}
