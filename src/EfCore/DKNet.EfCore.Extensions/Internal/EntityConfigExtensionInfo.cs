namespace DKNet.EfCore.Extensions.Internal;

internal sealed class EntityConfigExtensionInfo(EntityAutoConfigRegister configRegister)
    : DbContextOptionsExtensionInfo(configRegister)
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

        // Order-independent: two registrations with the same assembly set and the same global model
        // builder set, listed in any order, must hash the same. Both drive the built model, so both must
        // vary the hash - otherwise EF Core can cache/reuse a model built from a different set.
        var assembliesHash = 0;
        foreach (var assembly in configRegister.Assemblies)
            assembliesHash ^= (assembly.FullName ?? assembly.GetName().Name ?? string.Empty)
                .GetHashCode(StringComparison.Ordinal);
        hash.Add(assembliesHash);

        // GlobalModelBuilders is a distinct set, so duplicate registrations cannot cancel out in the XOR.
        // Type hash matches the Type equality ShouldUseSameServiceProvider compares with.
        var buildersHash = 0;
        foreach (var builder in configRegister.GlobalModelBuilders) buildersHash ^= builder.GetHashCode();
        hash.Add(buildersHash);

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
        configRegister.Assemblies.ToHashSet().SetEquals(otherExtension.Assemblies) &&
        configRegister.GlobalModelBuilders.SetEquals(otherExtension.GlobalModelBuilders);

    #endregion
}