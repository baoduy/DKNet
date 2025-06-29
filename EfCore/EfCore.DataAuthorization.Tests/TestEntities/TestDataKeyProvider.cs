using System.Collections.Generic;
using System.Linq;
using DKNet.EfCore.DataAuthorization;

namespace EfCore.DataAuthorization.Tests.TestEntities;

internal class TestDataKeyProvider : IDataOwnerProvider
{
    private readonly string[] _ownedKeys = ["Steven"];

    public string GetOwnershipKey() => _ownedKeys.First();

    public IEnumerable<string> GetAccessibleKeys() => _ownedKeys;
}