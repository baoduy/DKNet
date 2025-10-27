using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ServiceBus;

public sealed class ServiceBusResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    #region Fields

    private EndpointReference? _primaryEndpoint;

    #endregion

    #region Properties

    private ReferenceExpression ConnectionString =>
        ReferenceExpression.Create(
            $"Endpoint=sb://{PrimaryEndpoint.Host};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");

    public ReferenceExpression ConnectionStringExpression =>
        this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation)
            ? connectionStringAnnotation.Resource.ConnectionStringExpression
            : ConnectionString;

    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, PrimaryEndpointName);

    #endregion

    #region Methods

    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default) =>
        this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation)
            ? connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken)
            : ConnectionString.GetValueAsync(cancellationToken);

    #endregion

    internal const string PrimaryEndpointName = "tcp";
    internal const string SecondaryEndpointName = "tcp2";
}