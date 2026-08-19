// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: EndpointConfigExtensions.cs
// Description: Options-driven discovery and registration of IEndpointConfig endpoint groups.

using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Asp.Versioning.Conventions;
using DKNet.Fw.Extensions.TypeExtractors;
using DKNet.SlimBus.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace DKNet.AspCore.Extensions;

/// <summary>
///     Per-registration options controlling how <see cref="IEndpointConfig" /> implementations are grouped and
///     mapped by <see cref="EndpointConfigExtensions.UseEndpointConfigs" />. Every default reproduces the DKNet
///     template's original hardcoded behaviour; a caller who supplies no options gets that behaviour unchanged.
/// </summary>
public sealed class EndpointRegistrationOptions
{
    /// <summary>
    ///     Builds the route pattern for a group from its <see cref="IEndpointConfig" />.
    ///     Defaults to <c>/v{version:apiVersion}{GroupEndpoint}</c>.
    /// </summary>
    public Func<IEndpointConfig, string> RouteTemplate { get; set; } =
        config => $"/v{{version:apiVersion}}{config.GroupEndpoint}";

    /// <summary>
    ///     Grouping tag applied when an <see cref="IEndpointConfig" /> resolves an empty <see cref="IEndpointConfig.Tag" />.
    ///     Defaults to <c>"Root"</c>.
    /// </summary>
    public string DefaultTag { get; set; } = "Root";

    /// <summary>
    ///     Whether registered groups require authorization. Defaults to <see langword="true" />; disabling it is an
    ///     explicit per-host opt-out (see Rule R1).
    /// </summary>
    public bool RequireAuthorization { get; set; } = true;

    /// <summary>
    ///     Whether registered groups get FluentValidation auto-validation applied. Defaults to <see langword="true" />;
    ///     a host that disables this handles request validation itself.
    /// </summary>
    public bool EnableRequestValidation { get; set; } = true;

    /// <summary>
    ///     The <see cref="RequestBase.ByUser" /> value stamped on requests when <see cref="RequireAuthorization" /> is
    ///     <see langword="false" />. Defaults to <c>"system"</c>.
    /// </summary>
    public string SystemAccountName { get; set; } = "system";
}

/// <summary>
///     Discovers <see cref="IEndpointConfig" /> implementations and maps each one to a versioned
///     <see cref="RouteGroupBuilder" />.
/// </summary>
public static class EndpointConfigExtensions
{
    extension(WebApplication app)
    {
        /// <summary>
        ///     Discovers every non-abstract, non-generic <see cref="IEndpointConfig" /> implementation across
        ///     <paramref name="assemblies" /> — or every currently loaded assembly when none are supplied, so
        ///     endpoint declarations in the consuming application are found alongside the package's own — builds a
        ///     versioned <see cref="RouteGroupBuilder" /> per config, and calls <see cref="IEndpointConfig.Map" /> on it.
        /// </summary>
        /// <param name="configureOptions">Overrides the registration defaults; leave <see langword="null" /> to keep them.</param>
        /// <param name="assemblies">Assemblies to scan for <see cref="IEndpointConfig" /> implementations.</param>
        /// <returns>The route groups created, one per discovered config.</returns>
        public IReadOnlyList<RouteGroupBuilder> UseEndpointConfigs(
            Action<EndpointRegistrationOptions>? configureOptions = null,
            params Assembly[] assemblies)
        {
            var options = new EndpointRegistrationOptions();
            configureOptions?.Invoke(options);

            var scanAssemblies = assemblies.Length > 0 ? assemblies : AppDomain.CurrentDomain.GetAssemblies();
            var configs = scanAssemblies
                .Extract()
                .NotGeneric()
                .NotAbstract()
                .Classes()
                .IsInstanceOf<IEndpointConfig>()
                .Select(t => (IEndpointConfig)Activator.CreateInstance(t)!)
                .ToList();

            app.Logger.LogInformation(
                "UseEndpointConfigs discovered {Count} endpoint configuration(s).", configs.Count);

            if (configs.Count == 0) return [];

            var versionSet = app.NewApiVersionSet()
                .HasApiVersions(configs.Select(c => c.Version).Distinct().Select(v => new ApiVersion(v)))
                .ReportApiVersions()
                .Build();

            return [.. configs.Select(config => app.MapEndpointConfig(config, versionSet, options))];
        }

        private RouteGroupBuilder MapEndpointConfig(
            IEndpointConfig config,
            ApiVersionSet versionSet,
            EndpointRegistrationOptions options)
        {
            var group = app.MapGroup(options.RouteTemplate(config))
                .WithApiVersionSet(versionSet)
                .HasApiVersion(config.Version)
                .MapToApiVersion(config.Version)
                .WithDisplayName($"v{config.Version}{config.GroupEndpoint}")
                .WithGroupName($"v{config.Version}")
                .WithTags(string.IsNullOrEmpty(config.Tag) ? options.DefaultTag : config.Tag)
                .AddEndpointFilter(async (context, next) =>
                {
                    var identity = context.HttpContext.User.Identity;
                    var userName = options.RequireAuthorization
                        ? identity is { IsAuthenticated: true } ? identity.Name : null
                        : options.SystemAccountName;

                    foreach (var argument in context.Arguments)
                        if (argument is RequestBase requestBase)
                            requestBase.ByUser = userName;

                    return await next(context);
                });

            if (options.EnableRequestValidation)
                group.AddFluentValidationAutoValidation();

            if (options.RequireAuthorization)
                group = string.IsNullOrEmpty(config.AuthPolicy)
                    ? group.RequireAuthorization()
                    : group.RequireAuthorization(config.AuthPolicy);

            config.Map(group);
            return group;
        }
    }
}
