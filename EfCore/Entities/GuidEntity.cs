using System;

namespace EfCore.TestDataLayer;

public class GuidEntity : Entity
{
    public string Name { get; set; }
}

public class GuidAuditEntity() : AuditedEntity(Guid.Empty, "Unit Test")
{
    public string Name { get; set; } = "Testing";
}