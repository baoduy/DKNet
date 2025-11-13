// <copyright file="ServiceBusResource.cs" company="drunkcoding.net">
// Copyright (c) drunkcoding.net. All rights reserved.
// </copyright>

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.ServiceBus;

/// <summary>
///     Represents an Azure Service Bus emulator resource for Aspire distributed applications.
/// </summary>
/// <param name="name">The name of the Service Bus resource.</param>
public sealed class ServiceBusResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    #region Fields

    private EndpointReference? _primaryEndpoint;

    #endregion

    #region Properties

    private ReferenceExpression ConnectionString =>
        ReferenceExpression.Create(
            $"Endpoint=sb://{PrimaryEndpoint.Host};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");

    /// <summary>
    ///     Gets the connection string expression for the Service Bus resource.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation)
            ? connectionStringAnnotation.Resource.ConnectionStringExpression
            : ConnectionString;

    /// <summary>
    ///     Gets the primary endpoint reference for the Service Bus resource.
    /// </summary>
    public EndpointReference PrimaryEndpoint =>
        _primaryEndpoint ??= new EndpointReference(this, PrimaryEndpointName);

    #endregion

    #region Methods

    /// <summary>
    ///     Gets the connection string for the Service Bus resource asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation that returns the connection string.</returns>
    public ValueTask<string?> GetConnectionStringAsync(CancellationToken cancellationToken = default) =>
        this.TryGetLastAnnotation<ConnectionStringRedirectAnnotation>(out var connectionStringAnnotation)
            ? connectionStringAnnotation.Resource.GetConnectionStringAsync(cancellationToken)
            : ConnectionString.GetValueAsync(cancellationToken);

    #endregion

    internal const string PrimaryEndpointName = "tcp";
    internal const string SecondaryEndpointName = "tcp2";
}