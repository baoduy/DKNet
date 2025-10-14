using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.DtoEntities.Features.StaticData;

public sealed class CurrencyData : IEntity<int>
{
    public CurrencyData(string code, string name, bool isCrypto) :
        this(0, code, name, isCrypto)
    {
    }

    internal CurrencyData(int id, string code, string name, bool isCrypto)
    {
        Id = id;
        Code = code;
        Name = name;
        IsCrypto = isCrypto;
    }

    private CurrencyData()
    {
        Id = 0;
        Code = string.Empty;
        Name = string.Empty;
    }

    public bool IsCrypto { get; set; }

    [MaxLength(10)] public string Code { get; private set; }

    [MaxLength(50)] public string Name { get; private set; }
    public int Id { get; private set; }

    public static CurrencyData CreateForSeed(int id, string code, string name, bool isCrypto) =>
        new(id, code, name, isCrypto);
}