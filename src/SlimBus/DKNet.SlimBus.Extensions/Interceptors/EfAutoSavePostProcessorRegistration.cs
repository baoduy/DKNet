using Microsoft.EntityFrameworkCore;

namespace DKNet.SlimBus.Extensions.Interceptors;

internal static class EfAutoSavePostProcessorRegistration
{
    #region Properties

    public static HashSet<Type> DbContextTypes { get; } = [];

    #endregion

    #region Methods

    public static void RegisterDbContextType<TDbContext>()
        where TDbContext : DbContext
    {
        DbContextTypes.Add(typeof(TDbContext));
    }

    #endregion
}