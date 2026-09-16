using Microsoft.EntityFrameworkCore;

namespace DKNet.SlimBus.Extensions.Interceptors;

internal interface IAutoSaveDbContextRegistration
{
    #region Properties

    Type DbContextType { get; }

    #endregion
}

// One resolvable registration per TDbContext, added to the service collection via TryAddEnumerable so
// registering the same TDbContext twice on one collection collapses to a single entry. Container-scoped:
// each ServiceProvider only ever resolves the DbContext types registered on its own collection.
internal sealed class AutoSaveDbContextRegistration<TDbContext> : IAutoSaveDbContextRegistration
    where TDbContext : DbContext
{
    #region Properties

    public Type DbContextType => typeof(TDbContext);

    #endregion
}
