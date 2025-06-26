using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EfCore.TestDataLayer;

[Owned]
[Table("OwnedEntities")]
public class OwnedEntity
{
    #region Fields

    private readonly string privateField;

    #endregion Fields

    #region Methods

    public string GetPrivate()
    {
        return privateField;
    }

    #endregion Methods

    #region Constructors

    public OwnedEntity(string internalProp, string privateField)
    {
        this.privateField = privateField;
        InternalProp = internalProp;
    }

    internal OwnedEntity()
    {
    }

    #endregion Constructors

    #region Properties

    public string FullName => $"{nameof(OwnedEntity)} {Name}";

    [Key] public int Id { get; set; }

    public string Name { get; set; }

    [ReadOnly(false)] public string NotReadOnly { get; set; }

    [ReadOnly(true)] public string ReadOnly { get; set; }

    // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local
    internal string InternalProp { get; private set; }

    #endregion Properties
}