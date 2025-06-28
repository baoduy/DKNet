using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EfCore.TestDataLayer;

[Owned]
[Table("OwnedEntities")]
public class OwnedEntity(string internalProp, string privateField, string name, string notReadOnly, string readOnly)
{
    public string GetPrivate()
    {
        return privateField;
    }


    public string FullName => $"{nameof(OwnedEntity)} {Name}";

    [Key] public int Id { get; set; }

    public string Name { get; set; } = name;

    [ReadOnly(false)] public string NotReadOnly { get; set; } = notReadOnly;

    [ReadOnly(true)] public string ReadOnly { get; set; } = readOnly;

    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    internal string InternalProp { get; private set; } = internalProp;
}