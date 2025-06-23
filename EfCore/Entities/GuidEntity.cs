using System;
using DKNet.EfCore.Abstractions.Entities;

namespace EfCore.TestDataLayer;

public class GuidEntity : Entity
{
    public string Name { get; set; }
}

public class GuidAuditEntity() : AuditedEntity(Guid.Empty, "Unit Test")
{
    public string Name { get; set; } = "Testing";
}