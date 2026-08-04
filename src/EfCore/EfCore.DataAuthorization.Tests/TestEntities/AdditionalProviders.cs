using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests.TestEntities;

/// <summary>
///     A data owner provider that returns an empty ownership key, simulating an unauthenticated or
///     system-level context where ownership is not assigned.
/// </summary>
internal class EmptyOwnerKeyProvider : IDataOwnerProvider
{
    public ICollection<string> GetAccessibleKeys() => ["Steven"];

    public string GetOwnershipKey() => string.Empty;
}

/// <summary>
///     A data owner provider with no accessible keys, simulating a context that has not been granted
///     access to any owned data — the deny-all default, since it does not opt into unrestricted access.
/// </summary>
internal class EmptyAccessibleKeysProvider : IDataOwnerProvider
{
    public ICollection<string> GetAccessibleKeys() => [];

    public string GetOwnershipKey() => "Steven";
}

/// <summary>
///     A data owner provider with specific accessible keys, used to verify that
///     unrestricted access bypasses these restrictions.
/// </summary>
internal class NonEmptyAccessibleKeysProvider : IDataOwnerProvider
{
    public ICollection<string> GetAccessibleKeys() => ["Steven"];

    public string GetOwnershipKey() => "Steven";
}
