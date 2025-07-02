using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Extensions.Tests;


public class EntityTypesTests
{

    [Fact]
    public void TestEntityAudit()
    {
        typeof(Entity<int>).IsAssignableFrom(typeof(AuditedEntity<int>))
            .ShouldBeTrue();

        typeof(Entity<int>).IsAssignableFrom(typeof(AuditedEntity<int>))
            .ShouldBeTrue();

        typeof(IEntity<int>).IsAssignableFrom(typeof(AuditedEntity<int>))
            .ShouldBeTrue();

        typeof(IEntity<int>).IsAssignableFrom(typeof(AuditedEntity<int>))
            .ShouldBeTrue();
    }

    [Fact]
    public void TestEntityAuditEntityGeneric()
    {
        typeof(IEntity<int>).IsAssignableFrom(typeof(IAuditedEntity<int>))
            .ShouldBeTrue();
    }

    [Fact]
    public void TestEntityAuditGeneric()
    {
        typeof(IEntity<int>).IsAssignableFrom(typeof(IAuditedEntity<int>))
            .ShouldBeTrue();
    }

    [Fact]
    public void TestEntityGenericAuditGeneric()
    {
        typeof(IEntity<int>).IsAssignableFrom(typeof(IAuditedEntity<int>))
            .ShouldBeTrue();
    }

    [Fact]
    public void TestIAuditAudit()
    {
        typeof(IAuditedEntity<int>).IsAssignableFrom(typeof(AuditedEntity<int>))
            .ShouldBeTrue();

        typeof(IAuditedEntity<Guid>).IsAssignableFrom(typeof(AuditedEntity))
            .ShouldBeTrue();
    }

    [Fact]
    public void TestIEntityEntity()
    {
        typeof(IEntity<int>).IsAssignableFrom(typeof(Entity<int>))
            .ShouldBeTrue();

        typeof(IEntity<Guid>).IsAssignableFrom(typeof(Entity))
            .ShouldBeTrue();
    }

    [Fact]
    public void TestIEntityIEntityGeneric()
    {
        typeof(IEntity<int>).IsAssignableFrom(typeof(IEntity<int>))
            .ShouldBeTrue();
    }
    
}