using DKNet.EfCore.Extensions.Extensions;

namespace EfCore.Extensions.Tests;

#pragma warning disable CA2012 // Use ValueTasks correctly

/// <summary>
///     DRK-2522: Postgres <c>nextval()</c> always returns <c>bigint</c>, so the typed
///     <c>NextSeqValue&lt;TEnum, TValue&gt;</c> must convert it to a narrower <c>TValue</c> when the value fits and
///     throw <see cref="OverflowException" /> when it does not.
/// </summary>
public class NpgsqlTypedSequenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public async Task NextSeqValue_BigintReadAsInt_ReturnsValue()
    {
        await _db.Database.ExecuteSqlRawAsync("SELECT setval('seq.\"Seq_Invoice\"', 41)");

        var val = await _db.NextSeqValue<SequencesTest, int>(SequencesTest.Invoice);

        val.ShouldBe(42);
    }

    [Fact]
    public async Task NextSeqValue_ValueAboveIntMax_ThrowsOverflowException()
    {
        await _db.Database.ExecuteSqlRawAsync("SELECT setval('seq.\"Seq_Payment\"', 3000000000)");

        await Should.ThrowAsync<OverflowException>(async () =>
            await _db.NextSeqValue<SequencesTest, int>(SequencesTest.Payment));
    }

    #endregion
}
