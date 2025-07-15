using System.ComponentModel.DataAnnotations;

namespace DKNet.EfCore.DataAuthorization;

public interface IOwnedBy
{
    /// <summary>
    ///     Multi tenant owner and allow null. Null means there is no ownership or sharing entity for all tenants
    /// </summary>
    [MaxLength(1000)]
    string? OwnedBy { get; }

    /// <summary>
    ///     Update the owner this only call when creating the object. Or change the ownership of entity
    /// </summary>
    /// <param name="ownerKey"></param>
    void SetOwnedBy(string ownerKey);
}