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
///     A data owner provider with no accessible keys, simulating a super-user context where
///     all entities should be visible regardless of ownership.
/// </summary>
internal class EmptyAccessibleKeysProvider : IDataOwnerProvider
{
    public ICollection<string> GetAccessibleKeys() => [];

    public string GetOwnershipKey() => "Steven";
}
