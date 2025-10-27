using DKNet.EfCore.DataAuthorization;

namespace EfCore.Events.Tests.TestEntities;

internal class TestDataKeyProvider : IDataOwnerProvider
{
    #region Fields

    private readonly string[] _ownedKeys = ["Steven"];

    #endregion

    #region Methods

    public ICollection<string> GetAccessibleKeys() => _ownedKeys;

    public string GetOwnershipKey() => _ownedKeys[0];

    #endregion
}