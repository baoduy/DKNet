namespace DKNet.EfCore.Extensions.Internal;

internal sealed class EntityConfigRegisterService(EntityAutoConfigRegister entityConfig)
{
    public EntityAutoConfigRegister EntityConfig { get; } =
        entityConfig ?? throw new ArgumentNullException(nameof(entityConfig));
}