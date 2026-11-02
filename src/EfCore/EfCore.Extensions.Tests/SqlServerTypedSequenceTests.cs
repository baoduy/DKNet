using DKNet.EfCore.Extensions.Extensions;

namespace EfCore.Extensions.Tests;

#pragma warning disable CA2012 // Use ValueTasks correctly

/// <summary>
///     DRK-2522: the typed <c>NextSeqValue&lt;TEnum, TValue&gt;</c> must convert the provider's value to
///     <c>TValue</c>, not unbox it. SQL Server returns the sequence's declared type (<c>Int16</c> for Invoice).
/// </summary>
public class SqlServerTypedSequenceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public async Task NextSeqValue_ShortSequenceReadAsLong_ReturnsValue()
    {
        await _db.Database.ExecuteSqlRawAsync("ALTER SEQUENCE seq.Seq_Invoice RESTART WITH 1234");

        var val = await _db.NextSeqValue<SequencesTest, long>(SequencesTest.Invoice);

        val.ShouldBe(1234L);
    }

    [Fact]
    public async Task NextSeqValue_EnumWithoutSqlSequence_ReturnsNull()
    {
        var val = await _db.NextSeqValue<DayOfWeek, int>(DayOfWeek.Monday);

        val.ShouldBeNull();
    }

    #endregion
}
