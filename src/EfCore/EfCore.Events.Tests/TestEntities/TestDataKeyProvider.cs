using DKNet.EfCore.DataAuthorization;

namespace EfCore.Events.Tests.TestEntities;

internal class TestDataKeyProvider : IDataOwnerProvider
{
    private readonly string[] _ownedKeys = ["Steven"];

    public string GetOwnershipKey() => _ownedKeys[0];

    public ICollection<string> GetAccessibleKeys() => _ownedKeys;
}