namespace DKNet.EfCore.Extensions.Internal;

internal sealed class EntityConfigExtensionInfo(EntityAutoConfigRegister extension) : DbContextOptionsExtensionInfo(extension)
{
    public override bool IsDatabaseProvider => false;

    public override string LogFragment => $"using {nameof(EntityAutoConfigRegister)}";

    public override int GetServiceProviderHashCode() => 
        nameof(EntityAutoConfigRegister).GetHashCode(StringComparison.Ordinal);
    public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;

    public override void PopulateDebugInfo(IDictionary<string, string>? debugInfo)
    {
        if (debugInfo is not null)
            debugInfo["Core:" + nameof(EntityAutoConfigRegister)] =
                GetServiceProviderHashCode().ToString(CultureInfo.CurrentCulture);
    }
}