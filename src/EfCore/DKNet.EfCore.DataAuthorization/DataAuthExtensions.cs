using DKNet.EfCore.DataAuthorization.Internals;

namespace DKNet.EfCore.DataAuthorization;

internal static class DataAuthExtensions
{
    public static string GetQueryFilterKey<TEntity>() =>
        $"{typeof(TEntity).FullName}_{nameof(DataOwnerAuthQueryRegister)}";
}