namespace DKNet.EfCore.Extensions.Internal;

internal sealed class EntityConfigExtensionInfo(EntityAutoConfigRegister extension)
    : DbContextOptionsExtensionInfo(extension)
{
    #region Properties

    public override bool IsDatabaseProvider => false;

    public override string LogFragment => $"using {nameof(EntityAutoConfigRegister)}";

    #endregion

    #region Methods

    public override int GetServiceProviderHashCode()
    {
        var hash = new HashCode();
        hash.Add(nameof(EntityAutoConfigRegister), StringComparer.Ordinal);

        // Order-independent: two registrations with the same assembly set, listed in any order,
        // must hash the same. The assembly list drives the built model, so it must vary the hash -
        // otherwise EF Core can cache/reuse a model built from a different assembly set.
        var assembliesHash = 0;
        foreach (var assembly in extension.Assemblies)
            assembliesHash ^= (assembly.FullName ?? assembly.GetName().Name ?? string.Empty)
                .GetHashCode(StringComparison.Ordinal);
        hash.Add(assembliesHash);

        return hash.ToHashCode();
    }

    public override void PopulateDebugInfo(IDictionary<string, string>? debugInfo)
    {
        if (debugInfo is not null)
            debugInfo["Core:" + nameof(EntityAutoConfigRegister)] =
                GetServiceProviderHashCode().ToString(CultureInfo.CurrentCulture);
    }

    public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
        other is EntityConfigExtensionInfo { Extension: EntityAutoConfigRegister otherExtension } &&
        extension.Assemblies.ToHashSet().SetEquals(otherExtension.Assemblies);

    #endregion
}