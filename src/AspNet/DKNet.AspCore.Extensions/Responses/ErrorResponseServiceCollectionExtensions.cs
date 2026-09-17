// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseServiceCollectionExtensions.cs
// Description: The one registration point for DRK-1484's error-response setting — applied to a failed command
// handler, refused validation input and an unhandled exception, with no second place a host can configure
// instead (R9: AddErrorResponses is the host's only step; it wires UseExceptionHandler() itself).

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace DKNet.AspCore.Extensions.Responses;

/// <summary>
///     Registers the one <see cref="ErrorResponseOptions" /> setting a host application uses to shape a failed
///     command handler's response, a refused validation input's response, and an unhandled exception's response.
/// </summary>
public static class ErrorResponseServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <see cref="ErrorResponseOptions" /> and wires it into the FluentResults command-failure path,
    ///     the FluentValidation input-refusal path, and unhandled-exception handling. A host calls this once; it
    ///     does not also need to call <c>UseExceptionHandler()</c>, <c>AddProblemDetails()</c>, or configure the
    ///     validation path separately.
    /// </summary>
    /// <param name="services">The service collection to register the setting into.</param>
    /// <param name="configure">Sets the setting's members; leave <see langword="null" /> to keep every default.</param>
    /// <returns>The same <see cref="IServiceCollection" />, for chaining.</returns>
    public static IServiceCollection AddErrorResponses(
        this IServiceCollection services,
        Action<ErrorResponseOptions>? configure = null)
    {
        var options = new ErrorResponseOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddFluentValidationAutoValidation(cfg =>
            cfg.OverrideDefaultResultFactoryWith<ErrorResponseValidationResultFactory>());

        services.AddProblemDetails();
        services.AddExceptionHandler<ErrorResponseExceptionHandler>();
        services.AddSingleton<IStartupFilter, UseErrorResponseExceptionHandlerStartupFilter>();

        return services;
    }

    /// <summary>
    ///     Inserts <c>UseExceptionHandler()</c> at the head of the pipeline so a host's only step is
    ///     <see cref="AddErrorResponses" /> — no second, start-up line is needed for unhandled exceptions to reach
    ///     <see cref="ErrorResponseExceptionHandler" />.
    /// </summary>
    private sealed class UseErrorResponseExceptionHandlerStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.UseExceptionHandler();
                next(app);
            };
    }
}
