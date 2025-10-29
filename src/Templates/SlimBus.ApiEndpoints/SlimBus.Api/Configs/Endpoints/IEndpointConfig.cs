// ReSharper disable once CheckNamespace

namespace Microsoft.AspNetCore.Builder;

public interface IEndpointConfig
{
    #region Properties

    int Version { get; }

    string GroupEndpoint { get; }

    #endregion

    #region Methods

    void Map(RouteGroupBuilder group);

    #endregion
}