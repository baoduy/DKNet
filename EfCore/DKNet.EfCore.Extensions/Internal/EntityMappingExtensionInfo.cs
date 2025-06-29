using System.Globalization;
using DKNet.EfCore.Extensions.Registers;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DKNet.EfCore.Extensions.Internal;

internal sealed class EntityMappingExtensionInfo(EntityAutoMappingRegister extension) : DbContextOptionsExtensionInfo(extension)
{
    public override bool IsDatabaseProvider => false;

    public override string LogFragment => $"using {nameof(EntityAutoMappingRegister)}";

    public override int GetServiceProviderHashCode() => 
        nameof(EntityAutoMappingRegister).GetHashCode(StringComparison.Ordinal);
    public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => true;

    public override void PopulateDebugInfo(IDictionary<string, string>? debugInfo)
    {
        if (debugInfo is not null)
            debugInfo["Core:" + nameof(EntityAutoMappingRegister)] =
                GetServiceProviderHashCode().ToString(CultureInfo.CurrentCulture);
    }
}