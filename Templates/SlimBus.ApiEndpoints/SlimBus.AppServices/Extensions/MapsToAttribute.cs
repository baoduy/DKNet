namespace SlimBus.AppServices.Extensions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MapsToAttribute(Type entityType) : Attribute
{
    public Type EntityType { get; } = entityType;
}