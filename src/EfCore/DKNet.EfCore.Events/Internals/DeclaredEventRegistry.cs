using System.Collections.Concurrent;
using System.Reflection;
using DKNet.EfCore.Abstractions.Events;

namespace DKNet.EfCore.Events.Internals;

/// <summary>
///     Resolves the <see cref="GeneratedEventAttribute" /> declarations an entity's assembly carries,
///     caching per-assembly so reflection only runs once for the lifetime of the process.
/// </summary>
internal static class DeclaredEventRegistry
{
    #region Fields

    private static readonly ConcurrentDictionary<Assembly, GeneratedEventAttribute[]> Cache = new();

    #endregion

    #region Methods

    /// <summary>
    ///     Gets the declared events registered for the given entity type.
    /// </summary>
    /// <param name="entityType">The runtime entity type to resolve declarations for.</param>
    /// <returns>The <see cref="GeneratedEventAttribute" /> declarations whose <c>EntityType</c> matches.</returns>
    public static IEnumerable<GeneratedEventAttribute> GetDeclaredEvents(Type entityType)
    {
        var assemblyAttributes = Cache.GetOrAdd(entityType.Assembly,
            static asm => asm.GetCustomAttributes<GeneratedEventAttribute>().ToArray());

        return assemblyAttributes.Where(a => a.EntityType == entityType);
    }

    #endregion
}
