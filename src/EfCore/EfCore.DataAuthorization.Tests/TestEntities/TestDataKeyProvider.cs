namespace EfCore.DataAuthorization.Tests.TestEntities;

internal class TestDataKeyProvider : IDataOwnerProvider
{
    #region Fields

    private readonly string[] _ownedKeys = ["Steven"];

    #endregion

    #region Methods

    public ICollection<string> GetAccessibleKeys() => this._ownedKeys;

    public string GetOwnershipKey() => this._ownedKeys[0];

    #endregion
}