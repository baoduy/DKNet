using System.Diagnostics.CodeAnalysis;
using DKNet.EfCore.DataAuthorization;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DDD4Tests;

/// <summary>
/// </summary>
/// <param name="options"></param>
/// <param name="dataKeyProviders">
///     optional <see cref="IDataOwnerProvider" /> injected from DI. Only first runner will be picked.
/// </param>
public class DddContext(DbContextOptions options,
    [AllowNull] IEnumerable<IDataOwnerProvider> dataKeyProviders) : DbContext(options), IDataOwnerDbContext
{
    //Internal fields will be available in unit test project.
    // ReSharper disable once MemberCanBePrivate.Global
    // ReSharper disable once InconsistentNaming
    internal readonly IDataOwnerProvider _dataKeyProvider = dataKeyProviders?.SingleOrDefault();

    public IEnumerable<string> AccessibleKeys => _dataKeyProvider?.GetAccessibleKeys() ?? [];

}