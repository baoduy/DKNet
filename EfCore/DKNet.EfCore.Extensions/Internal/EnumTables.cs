using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.Extensions.Internal;

internal sealed class EnumTables<T> : IEntity<int>
{
    public int Id { get; private set; }
}