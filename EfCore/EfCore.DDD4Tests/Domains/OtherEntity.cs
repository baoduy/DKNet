using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.DDD4Tests.Domains;

public class OtherEntity : Entity<int>
{
    public OtherEntity(string name)
    {
        Name = name;
    }

    private OtherEntity()
    {
    }

    public string Name { get; private set; }
}