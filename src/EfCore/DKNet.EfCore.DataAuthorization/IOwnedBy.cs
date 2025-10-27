namespace DKNet.EfCore.DataAuthorization;

public interface IOwnedBy
{
    #region Properties

    string OwnedBy { get; }

    #endregion
}