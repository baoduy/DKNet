using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.DtoEntities.Features.StaticData;

public sealed class CurrencyData : IEntity<int>
{
    #region Constructors

    public CurrencyData(string code, string name, bool isCrypto, string? description = null) :
        this(0, code, name, isCrypto, description)
    {
    }

    internal CurrencyData(int id, string code, string name, bool isCrypto, string? description = null)
    {
        this.Id = id;
        this.Code = code;
        this.Name = name;
        this.IsCrypto = isCrypto;
        this.Description = description;
    }

    private CurrencyData()
    {
        this.Id = 0;
        this.Code = string.Empty;
        this.Name = string.Empty;
    }

    #endregion

    #region Properties

    public bool IsCrypto { get; set; }

    public int Id { get; private set; }

    [MaxLength(10)] public string Code { get; private set; }

    [MaxLength(50)] public string Name { get; private set; }

    [MaxLength(200)] public string? Description { get; private set; }

    #endregion

    #region Methods

    public static CurrencyData CreateForSeed(int id, string code, string name, bool isCrypto) =>
        new(id, code, name, isCrypto);

    #endregion
}