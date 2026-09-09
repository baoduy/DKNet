// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ListQueryOptions.cs
// Description: Host-configurable defaults and ceiling for the generic list endpoints' page size, plus its
//              DI registration.

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     Host-configurable page-size defaults for every generic list endpoint mapped by
///     <see cref="FluentsEntityEndpointMapperExtensions" />. Bind the <see cref="ConfigSectionName" /> section or
///     configure via <see cref="ListQueryOptionsServiceCollectionExtensions.AddListQueryOptions" />; an app that
///     does neither still resolves <see cref="Microsoft.Extensions.Options.IOptions{TOptions}" /> to these defaults.
/// </summary>
public sealed class ListQueryOptions
{
    /// <summary>Configuration section name this options type binds to: <c>DKNet:ListQuery</c>.</summary>
    public const string ConfigSectionName = "DKNet:ListQuery";

    /// <summary>Page size applied when the caller does not ask for one. Defaults to 1000.</summary>
    [Range(1, int.MaxValue)]
    public int DefaultPageSize { get; set; } = 1000;

    /// <summary>Largest page a caller may request, whatever they ask for. Defaults to 1000.</summary>
    [Range(1, int.MaxValue)]
    public int MaxPageSize { get; set; } = 1000;

    /// <summary>
    ///     Width, in months, of the default recent-activity window applied to a <c>MapGetList</c> request that
    ///     names neither <c>fromDate</c> nor <c>toDate</c>, for a listed type that carries audit timestamps.
    ///     Defaults to 3. <c>0</c> switches the window off, so an unbounded request is served in full.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int DefaultActivityWindowMonths { get; set; } = 3;
}

/// <summary>Registers <see cref="ListQueryOptions" /> on <see cref="IServiceCollection" />.</summary>
public static class ListQueryOptionsServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <see cref="ListQueryOptions" />, validating it on startup rather than letting an invalid
    ///     value degrade a running endpoint. Calling this is optional — an app that never calls it, and never
    ///     binds <see cref="ListQueryOptions.ConfigSectionName" />, still resolves the type's defaults.
    /// </summary>
    /// <param name="services">The service collection to register against.</param>
    /// <param name="configure">Configures <see cref="ListQueryOptions" />; leave <see langword="null" /> for defaults.</param>
    /// <returns><paramref name="services" />, for chaining.</returns>
    public static IServiceCollection AddListQueryOptions(
        this IServiceCollection services,
        Action<ListQueryOptions>? configure = null)
    {
        var builder = services.AddOptions<ListQueryOptions>();
        if (configure is not null) builder.Configure(configure);

        builder.ValidateDataAnnotations().ValidateOnStart();

        return services;
    }
}
