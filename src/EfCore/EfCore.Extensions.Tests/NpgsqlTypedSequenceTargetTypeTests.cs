namespace EfCore.Extensions.Tests;

/// <summary>
///     DRK-2523: the typed <c>NextSeqValue&lt;TEnum, TValue&gt;</c> converts to any integer type; a bool or
///     floating-point <c>TValue</c> would reinterpret or round the value, so it still throws
///     <see cref="InvalidCastException" />.
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
    public async Task NextSeqValue_ReadAsBool_ThrowsInvalidCastException()
    {
        var ex = await Should.ThrowAsync<InvalidCastException>(async () =>
            await _db.NextSeqValue<SequencesTest, bool>(SequencesTest.Payment));

        ex.Message.ShouldBe(
            "Sequence value of type System.Int64 cannot be converted to non-integer type System.Boolean.");
    }

    [Fact]
    public async Task NextSeqValue_ReadAsFloat_ThrowsInvalidCastException()
    {
        await Should.ThrowAsync<InvalidCastException>(async () =>
            await _db.NextSeqValue<SequencesTest, float>(SequencesTest.Payment));
    }

    #endregion
}
