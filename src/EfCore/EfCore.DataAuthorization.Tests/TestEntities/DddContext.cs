using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests.TestEntities;

/// <summary>
/// </summary>
/// <param name="options"></param>
/// <param name="dataKeyProviders">
///     optional <see cref="IDataOwnerProvider" /> injected from DI. Only first runner will be picked.
/// </param>
public class DddContext(
    DbContextOptions options,
    IEnumerable<IDataOwnerProvider> dataKeyProviders) : DbContext(options), IDataOwnerDbContext
{
    #region Properties

    public ICollection<string> AccessibleKeys => _dataKeyProvider?.GetAccessibleKeys() ?? [];

    #endregion

    //Internal fields will be available in unit test project.
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once InconsistentNaming
    internal readonly IDataOwnerProvider _dataKeyProvider = dataKeyProviders?.First();
}