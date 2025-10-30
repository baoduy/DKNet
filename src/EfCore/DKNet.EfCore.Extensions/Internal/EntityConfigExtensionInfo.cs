namespace DKNet.EfCore.Extensions.Internal;

internal sealed class EntityConfigExtensionInfo(EntityAutoConfigRegister extension)
    : DbContextOptionsExtensionInfo(extension)
{
    #region Properties

    public override bool IsDatabaseProvider => false;

    public override string LogFragment => $"using {nameof(EntityAutoConfigRegister)}";

    #endregion

    #region Methods

    public override int GetServiceProviderHashCode() =>
        nameof(EntityAutoConfigRegister).GetHashCode(StringComparison.Ordinal);

    public override void PopulateDebugInfo(IDictionary<string, string>? debugInfo)
    {
        if (debugInfo is not null)
        {
            debugInfo["Core:" + nameof(EntityAutoConfigRegister)] =
                this.GetServiceProviderHashCode().ToString(CultureInfo.CurrentCulture);
        }
    }

    public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;

    #endregion
}