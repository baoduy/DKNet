using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EfCore.TestDataLayer;

[Owned]
[Table("OwnedEntities")]
public class OwnedEntity
{
    public string FullName => $"{nameof(OwnedEntity)} {Name}";

    public string? Name { get; set; }
}