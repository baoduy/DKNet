using DKNet.EfCore.Abstractions.Entities;

namespace AspCore.Extensions.Tests.TestEntities;

/// <summary>
///     <see cref="int" />-keyed entity used to exercise the <c>TKey</c>-generic <c>MapGetById</c> /
///     <c>MapGetList</c> / <c>MapDeleteById</c> overloads against a key type that is a value type but not
///     <see cref="Guid" />, so the minimal-API route binding and the generic id-equality predicate are both
///     covered for a struct key the framework does not special-case.
/// </summary>
public sealed class SprocketEntity : Entity<int>
{
    #region Constructors

    public SprocketEntity()
    {
    }

    public SprocketEntity(int id, string name) : base(id) => Name = name;

    #endregion

    #region Properties

    public string Name { get; set; } = string.Empty;

    #endregion
}

/// <summary>Projection model <see cref="SprocketEntity" /> maps to by convention (matching property names).</summary>
public sealed class SprocketModel
{
    #region Properties

    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    #endregion
}

/// <summary>
///     <see cref="string" />-keyed entity. <see cref="string" /> is deliberately covered because it does not
///     implement <see cref="IParsable{TSelf}" /> — it is the case that would be excluded had the mappers
///     constrained <c>TKey</c> to <see cref="IParsable{TSelf}" />, and minimal APIs bind it natively instead.
/// </summary>
public sealed class CouponEntity : Entity<string>
{
    #region Constructors

    public CouponEntity()
    {
    }

    public CouponEntity(string id, string label) : base(id) => Label = label;

    #endregion

    #region Properties

    public string Label { get; set; } = string.Empty;

    #endregion
}

/// <summary>Projection model <see cref="CouponEntity" /> maps to by convention (matching property names).</summary>
public sealed class CouponModel
{
    #region Properties

    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    #endregion
}
