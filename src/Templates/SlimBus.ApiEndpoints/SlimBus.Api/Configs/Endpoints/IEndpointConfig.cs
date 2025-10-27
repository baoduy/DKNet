// ReSharper disable once CheckNamespace

namespace Microsoft.AspNetCore.Builder;

public interface IEndpointConfig
{
    #region Properties

    string GroupEndpoint { get; }
    int Version { get; }

    #endregion

    #region Methods

    void Map(RouteGroupBuilder group);

    #endregion
}