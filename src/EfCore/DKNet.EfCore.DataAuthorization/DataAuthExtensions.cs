using DKNet.EfCore.DataAuthorization.Internals;

namespace DKNet.EfCore.DataAuthorization;

internal static class DataAuthExtensions
{
    #region Methods

    public static string GetQueryFilterKey<TEntity>() =>
        $"{typeof(TEntity).FullName}_{nameof(DataOwnerAuthQuery)}";

    #endregion
}