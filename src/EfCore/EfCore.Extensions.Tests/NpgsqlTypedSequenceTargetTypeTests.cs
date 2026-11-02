namespace EfCore.Extensions.Tests;

/// <summary>
///     DRK-2523: the typed <c>NextSeqValue&lt;TEnum, TValue&gt;</c> converts to any integer or decimal type; a
///     bool, floating-point or enum <c>TValue</c> throws <see cref="InvalidCastException" /> before the sequence is
///     read.
/// </summary>
public class NpgsqlTypedSequenceTargetTypeTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public async Task NextSeqValue_ReadAsByte_ReturnsValue()
    {
        await _db.Database.ExecuteSqlRawAsync("SELECT setval('seq.\"Seq_Invoice\"', 199)");

        var val = await _db.NextSeqValue<SequencesTest, byte>(SequencesTest.Invoice);

        val.ShouldBe((byte)200);
    }

    [Fact]
    public async Task NextSeqValue_ReadAsDecimal_ReturnsValue()
    {
        await _db.Database.ExecuteSqlRawAsync("SELECT setval('seq.\"Seq_Payment\"', 199)");

        var val = await _db.NextSeqValue<SequencesTest, decimal>(SequencesTest.Payment);

        val.ShouldBe(200m);
    }

    [Fact]
    public async Task NextSeqValue_ReadAsBool_ThrowsInvalidCastException()
    {
        var ex = await Should.ThrowAsync<InvalidCastException>(async () =>
            await _db.NextSeqValue<SequencesTest, bool>(SequencesTest.Payment));

        ex.Message.ShouldBe("Sequence values cannot be converted to System.Boolean; use an integer or decimal type.");
    }

    [Fact]
    public async Task NextSeqValue_ReadAsEnum_ThrowsInvalidCastException()
    {
        var ex = await Should.ThrowAsync<InvalidCastException>(async () =>
            await _db.NextSeqValue<SequencesTest, DayOfWeek>(SequencesTest.Payment));

        ex.Message.ShouldBe("Sequence values cannot be converted to System.DayOfWeek; use an integer or decimal type.");
    }

    [Fact]
    public async Task NextSeqValue_RejectedTargetType_DoesNotUseUpASequenceValue()
    {
        await _db.Database.ExecuteSqlRawAsync("SELECT setval('seq.\"Seq_Invoice\"', 299)");

        await Should.ThrowAsync<InvalidCastException>(async () =>
            await _db.NextSeqValue<SequencesTest, bool>(SequencesTest.Invoice));
        var val = await _db.NextSeqValue<SequencesTest, int>(SequencesTest.Invoice);

        val.ShouldBe(300);
    }

    [Fact]
    public async Task NextSeqValue_ReadAsFloat_ThrowsInvalidCastException()
    {
        await Should.ThrowAsync<InvalidCastException>(async () =>
            await _db.NextSeqValue<SequencesTest, float>(SequencesTest.Payment));
    }

    #endregion
}
