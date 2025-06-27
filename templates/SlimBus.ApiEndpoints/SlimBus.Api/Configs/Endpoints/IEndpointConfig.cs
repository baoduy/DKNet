// ReSharper disable once CheckNamespace

namespace Microsoft.AspNetCore.Builder;

public interface IEndpointConfig
{
    string GroupEndpoint { get; }
    int Version { get; }

    void Map(RouteGroupBuilder group);
}