using DKNet.EfCore.DataAuthorization;

namespace EfCore.DDD4Tests;

public sealed class TestDataKeyProvider : IDataOwnerProvider
{
    public IEnumerable<string> GetAccessibleKeys() => [GetOwnershipKey()];

    public string GetOwnershipKey() => "bc2c7648-6e0e-41f9-adff-b344302fdc8d";
}