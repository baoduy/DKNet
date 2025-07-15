namespace SlimBus.AppHost.Resources;

internal sealed class ServiceBusResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "tcp";

    private EndpointReference? _primaryEndpoint;

    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, PrimaryEndpointName);

    private ReferenceExpression ConnectionString =>
        ReferenceExpression.Create(
            $"Endpoint=sb://{PrimaryEndpoint.Host};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");

    public ReferenceExpression ConnectionStringExpression =>
        this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation)
            ? connectionStringAnnotation.Resource.ConnectionStringExpression
            : ConnectionString;

    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default) =>
        this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation)
            ? connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken)
            : ConnectionString.GetValueAsync(cancellationToken);
}