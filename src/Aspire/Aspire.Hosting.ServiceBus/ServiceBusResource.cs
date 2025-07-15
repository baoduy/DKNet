using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ServiceBus;

public sealed class ServiceBusResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "tcp";
    internal const string SecondaryEndpointName = "tcp2";

    private EndpointReference? _primaryEndpoint;

    public EndpointReference PrimaryEndpoint
    {
        get => _primaryEndpoint ??= new EndpointReference(this, PrimaryEndpointName);
    }

    private ReferenceExpression ConnectionString
    {
        get => ReferenceExpression.Create(
            $"Endpoint=sb://{PrimaryEndpoint.Host};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");
    }

    public ReferenceExpression ConnectionStringExpression
    {
        get => this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation)
            ? connectionStringAnnotation.Resource.ConnectionStringExpression
            : ConnectionString;
    }

    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default) =>
        this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation)
            ? connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken)
            : ConnectionString.GetValueAsync(cancellationToken);
}