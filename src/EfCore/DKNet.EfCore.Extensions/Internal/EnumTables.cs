using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.Extensions.Internal;

internal sealed class EnumTables<TEnum> : IEntity<int> where TEnum : struct, Enum
{
    public int Id { get; private set; }
}