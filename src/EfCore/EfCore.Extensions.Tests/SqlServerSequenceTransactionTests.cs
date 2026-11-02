using System.Data;
using DKNet.EfCore.Extensions.Extensions;

namespace EfCore.Extensions.Tests;

#pragma warning disable CA2012 // Use ValueTasks correctly

/// <summary>
///     DRK-2485: on SQL Server a command created on the context's connection must be enlisted in EF's current
///     transaction, otherwise SqlClient refuses to run it. <c>NextSeqValue</c> must join that transaction.
/// </summary>
public class SqlServerSequenceTransactionTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    #region Fields

    private readonly MyDbContext _db = fixture.Db!;

    #endregion

    #region Methods

    [Fact]
    public async Task NextSeqValue_WithoutTransaction_ReturnsNextValue()
    {
        var val = await _db.NextSeqValue<SequencesTest, long>(SequencesTest.Payment);

        val!.Value.ShouldBeGreaterThan(0L);
    }

    [Fact]
    public async Task NextSeqValue_WithinExplicitTransaction_ReturnsNextValue()
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var user = new User("SqlSeqTxTest") { FirstName = "Seq", LastName = "Tx" };
        _db.Set<User>().Add(user);
        await _db.SaveChangesAsync();

        var val = await _db.NextSeqValue<SequencesTest, long>(SequencesTest.Payment);

        val!.Value.ShouldBeGreaterThan(0L);
        _db.Database.GetDbConnection().State.ShouldBe(ConnectionState.Open);

        await tx.CommitAsync();

        (await _db.Set<User>().CountAsync(u => u.Id == user.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task NextSeqValueWithFormat_WithinExplicitTransaction_ReturnsFormattedValue()
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        var val = await _db.NextSeqValueWithFormat(SequencesTest.Invoice);

        // Invoice format "T{DateTime:yyMMdd}{1:00000}": "T", the UTC date, then the value padded to 5 digits.
        val.ShouldMatch(@"^T\d{6}\d{5}$");
        val.ShouldStartWith(string.Format(CultureInfo.InvariantCulture, "T{0:yyMMdd}", DateTime.UtcNow));

        await tx.CommitAsync();
    }

    #endregion
}
