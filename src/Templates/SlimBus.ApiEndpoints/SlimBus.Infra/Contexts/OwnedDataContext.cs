using DKNet.EfCore.DataAuthorization;

namespace SlimBus.Infra.Contexts;

internal class OwnedDataContext(DbContextOptions options, IEnumerable<IDataOwnerProvider>? dataKeyProviders)
    : CoreDbContext(options), IDataOwnerDbContext
{
    #region Properties

    public IEnumerable<string> AccessibleKeys =>
        _dataKeyProvider is not null ? _dataKeyProvider.GetAccessibleKeys() : [];

    #endregion

    //Internal fields will be available in unit test project.
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once InconsistentNaming
    internal readonly IDataOwnerProvider? _dataKeyProvider = dataKeyProviders?.FirstOrDefault();
}