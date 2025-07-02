namespace EfCore.DataAuthorization.Tests.TestEntities;

internal class TestDataKeyProvider : IDataOwnerProvider
{
    private readonly string[] _ownedKeys = ["Steven"];

    public string GetOwnershipKey() => _ownedKeys[0];

    public IEnumerable<string> GetAccessibleKeys() => _ownedKeys;
}