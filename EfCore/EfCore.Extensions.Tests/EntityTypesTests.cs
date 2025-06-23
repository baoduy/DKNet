using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.Extensions.Tests;

[TestClass]
public class EntityTypesTests
{

    [TestMethod]
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

    [TestMethod]
    public void TestEntityAuditEntityGeneric()
    {
        typeof(IEntity<int>).IsAssignableFrom(typeof(IAuditedEntity<int>))
            .ShouldBeTrue();
    }

    [TestMethod]
    public void TestEntityAuditGeneric()
    {
        typeof(IEntity<int>).IsAssignableFrom(typeof(IAuditedEntity<int>))
            .ShouldBeTrue();
    }

    [TestMethod]
    public void TestEntityGenericAuditGeneric()
    {
        typeof(IEntity<int>).IsAssignableFrom(typeof(IAuditedEntity<int>))
            .ShouldBeTrue();
    }

    [TestMethod]
    public void TestIAuditAudit()
    {
        typeof(IAuditedEntity<int>).IsAssignableFrom(typeof(AuditedEntity<int>))
            .ShouldBeTrue();

        typeof(IAuditedEntity<Guid>).IsAssignableFrom(typeof(AuditedEntity))
            .ShouldBeTrue();
    }

    [TestMethod]
    public void TestIEntityEntity()
    {
        typeof(IEntity<int>).IsAssignableFrom(typeof(Entity<int>))
            .ShouldBeTrue();

        typeof(IEntity<Guid>).IsAssignableFrom(typeof(Entity))
            .ShouldBeTrue();
    }

    [TestMethod]
    public void TestIEntityIEntityGeneric()
    {
        typeof(IEntity<int>).IsAssignableFrom(typeof(IEntity<int>))
            .ShouldBeTrue();
    }
    
}