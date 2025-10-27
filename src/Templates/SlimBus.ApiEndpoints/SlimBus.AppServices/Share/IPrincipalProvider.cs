using DKNet.EfCore.DataAuthorization;

namespace SlimBus.AppServices.Share;

public interface IPrincipalProvider : IDataOwnerProvider
{
    #region Properties

    /// <summary>
    ///     User Email from Bearer Token
    /// </summary>
    string Email { get; }

    /// <summary>
    ///     The User Id from Bearer Token
    /// </summary>
    Guid ProfileId { get; }

    /// <summary>
    ///     User name from Bearer Token
    /// </summary>
    string UserName { get; }

    #endregion
}