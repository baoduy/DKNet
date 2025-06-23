using DKNet.EfCore.Extensions.Registers;

namespace DKNet.EfCore.Extensions.Internal;

internal sealed class EntityMappingRegisterService(EntityAutoMappingRegister entityMapping)
{
    public EntityAutoMappingRegister EntityMapping { get; } = entityMapping ?? throw new ArgumentNullException(nameof(entityMapping));
}