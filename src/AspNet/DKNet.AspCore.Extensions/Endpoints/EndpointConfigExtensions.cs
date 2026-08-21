// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: EndpointConfigExtensions.cs
// Description: Options-driven discovery and registration of IEndpointConfig endpoint groups.

using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Asp.Versioning.Conventions;
using DKNet.AspCore.Extensions.ModelBinding;
using DKNet.Fw.Extensions.TypeExtractors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DKNet.AspCore.Extensions.Endpoints;

/// <summary>
///     Per-registration options controlling how <see cref="IEndpointConfig" /> implementations are grouped and
///     mapped by <see cref="EndpointConfigExtensions.UseEndpointConfigs" />. Every default reproduces the DKNet
///     template's original hardcoded behaviour; a caller who supplies no options gets that behaviour unchanged.
/// </summary>
public sealed class EndpointRegistrationOptions
{
    /// <summary>
    ///     Builds the route pattern for a group from its <see cref="IEndpointConfig" />. Leave <see langword="null" />
    ///     to use the version-aware default: <c>/v{version:apiVersion}{GroupEndpoint}</c> when
    ///     <see cref="EnableVersioning" /> is <see langword="true" />, or <c>{GroupEndpoint}</c> when it is
    ///     <see langword="false" />.
    /// </summary>
    public Func<IEndpointConfig, string>? RouteTemplate { get; set; }

    /// <summary>
    ///     Grouping tag applied when an <see cref="IEndpointConfig" /> resolves an empty <see cref="IEndpointConfig.Tag" />.
    ///     Defaults to <c>"Root"</c>.
    /// </summary>
    public string DefaultTag { get; set; } = "Root";

    /// <summary>
    ///     Whether registered groups require authorization. Defaults to <see langword="true" />; disabling it is an
    ///     explicit per-host opt-out that the host itself owns.
    /// </summary>
    public bool RequireAuthorization { get; set; } = true;

    /// <summary>
    ///     Whether groups are mapped to a versioned route and carry API-version metadata. Defaults to
    ///     <see langword="true" />. When enabled, the host must have called <c>AddApiVersioning()</c> or
    ///     <see cref="EndpointConfigExtensions.UseEndpointConfigs" /> throws at startup — this guard is fail-fast and
    ///     fires regardless of discovery, even when zero <see cref="IEndpointConfig" /> implementations are found.
    /// </summary>
    public bool EnableVersioning { get; set; } = true;

    /// <summary>
    ///     Host callback invoked for every created group, after mapping/version metadata/tags are applied and before
    ///     authorization and <see cref="IEndpointConfig.Map" /> run. Use this to add host-specific setup — such as
    ///     request-user stamping or request validation — that used to be built into this package.
    /// </summary>
    public Action<RouteGroupBuilder, IEndpointConfig>? ConfigureGroup { get; set; }
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
        /// <exception cref="InvalidOperationException">
        ///     <see cref="EndpointRegistrationOptions.EnableVersioning" /> is <see langword="true" /> (the default) but
        ///     the host has not called <c>AddApiVersioning()</c>. This check is fail-fast and runs before endpoint
        ///     discovery, so it throws even when zero <see cref="IEndpointConfig" /> implementations are found.
        /// </exception>
        public IReadOnlyList<RouteGroupBuilder> UseEndpointConfigs(
            Action<EndpointRegistrationOptions>? configureOptions = null,
            params Assembly[] assemblies)
        {
            var options = new EndpointRegistrationOptions();
            configureOptions?.Invoke(options);

            if (options.EnableVersioning && app.Services.GetService<IApiVersionParser>() is null)
                throw new InvalidOperationException(
                    $"{nameof(EndpointRegistrationOptions.EnableVersioning)} is enabled but no API versioning " +
                    "services are registered. Call AddApiVersioning() on the service collection, or set " +
                    $"{nameof(EndpointRegistrationOptions.EnableVersioning)} to false.");

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

            var versionSet = options.EnableVersioning
                ? app.NewApiVersionSet()
                    .HasApiVersions(configs.Select(c => c.Version).Distinct().Select(v => new ApiVersion(v)))
                    .ReportApiVersions()
                    .Build()
                : null;

            return [.. configs.Select(config => app.MapEndpointConfig(config, versionSet, options))];
        }

        private RouteGroupBuilder MapEndpointConfig(
            IEndpointConfig config,
            ApiVersionSet? versionSet,
            EndpointRegistrationOptions options)
        {
            var routeTemplate = options.RouteTemplate is not null
                ? options.RouteTemplate(config)
                : options.EnableVersioning
                    ? $"/v{{version:apiVersion}}{config.GroupEndpoint}"
                    : config.GroupEndpoint;

            var group = app.MapGroup(routeTemplate);

            if (versionSet is not null)
                group = group
                    .WithApiVersionSet(versionSet)
                    .HasApiVersion(config.Version)
                    .MapToApiVersion(config.Version);

            group = group
                .WithDisplayName($"v{config.Version}{config.GroupEndpoint}")
                .WithGroupName($"v{config.Version}")
                .WithTags(string.IsNullOrEmpty(config.Tag) ? options.DefaultTag : config.Tag);

            // Registered first so it runs before any host filter added by ConfigureGroup (including a
            // validation filter) — the declared-member overwrite is unconditional and cannot be defeated by
            // ConfigureGroup's own registration order. IEndpointFilterFactory.Create runs once, at endpoint-build
            // time, so an IContextualSource-declared member with no setter (DKNet.AspCore.Extensions'
            // ContextualMemberScanner) fails fast at startup rather than on first request.
            group.AddEndpointFilterFactory((factoryContext, next) =>
            {
                // IServiceProviderIsService, not GetService: IContextualRequestPopulationService is scoped
                // (finding 3), and factoryContext.ApplicationServices is the root provider — resolving a scoped
                // service directly from it throws under ValidateScopes=true. Asking "is it registered" answers
                // the fail-fast question without instantiating anything from the wrong scope.
                var isServiceRegistered = factoryContext.ApplicationServices.GetService<IServiceProviderIsService>();
                foreach (var parameter in factoryContext.MethodInfo.GetParameters())
                {
                    var members = ContextualMemberScanner.GetDeclaredMembers(parameter.ParameterType);
                    if (members.Length > 0 &&
                        isServiceRegistered?.IsService(typeof(IContextualRequestPopulationService)) != true)
                        throw new InvalidOperationException(
                            $"'{parameter.ParameterType.FullName}' declares a contextual source (e.g. " +
                            $"{nameof(FromClaimAttribute)}) but " +
                            $"{nameof(ContextualRequestPopulationServiceCollectionExtensions.AddContextualRequestPopulation)}() " +
                            "was never called. Call it on the service collection, or remove the declaration.");
                }

                return async invocationContext =>
                {
                    var population = invocationContext.HttpContext.RequestServices
                        .GetService<IContextualRequestPopulationService>();
                    if (population is not null)
                        foreach (var argument in invocationContext.Arguments)
                            if (argument is not null)
                                population.Populate(argument, invocationContext.HttpContext, options.RequireAuthorization);

                    return await next(invocationContext);
                };
            });

            // Registration order only: host setup is applied before authorization is required below, so
            // host filters wrap the endpoint's own filters. At runtime those filters still execute after
            // the authorization middleware, so an unauthenticated or unauthorised request never reaches them.
            options.ConfigureGroup?.Invoke(group, config);

            if (options.RequireAuthorization)
                group = string.IsNullOrEmpty(config.AuthPolicy)
                    ? group.RequireAuthorization()
                    : group.RequireAuthorization(config.AuthPolicy);

            config.Map(group);
            return group;
        }
    }
}
