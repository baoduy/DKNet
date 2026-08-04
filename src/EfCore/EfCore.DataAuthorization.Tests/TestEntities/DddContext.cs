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

    public IEnumerable<string> AccessibleKeys => _dataKeyProvider?.GetAccessibleKeys() ?? [];

    public virtual bool IsUnrestrictedAccess => false;

    #endregion

    //Internal fields will be available in unit test project.
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once InconsistentNaming
    internal readonly IDataOwnerProvider _dataKeyProvider = dataKeyProviders.First();
}

/// <summary>
///     A context that explicitly opts into unrestricted access to bypass ownership filtering.
/// </summary>
public class UnrestrictedDddContext(
    DbContextOptions options,
    IEnumerable<IDataOwnerProvider> dataKeyProviders) : DddContext(options, dataKeyProviders)
{
    public override bool IsUnrestrictedAccess => true;
}
