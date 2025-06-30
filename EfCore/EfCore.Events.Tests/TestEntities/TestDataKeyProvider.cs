using System.Collections.Generic;
using DKNet.EfCore.DataAuthorization;

namespace EfCore.Events.Tests.TestEntities;

internal class TestDataKeyProvider : IDataOwnerProvider
{
    private readonly string[] _ownedKeys = ["Steven"];

    public string GetOwnershipKey() => _ownedKeys.First();

    public IEnumerable<string> GetAccessibleKeys() => _ownedKeys;
}