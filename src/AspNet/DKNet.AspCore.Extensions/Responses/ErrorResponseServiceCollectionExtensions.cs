// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseServiceCollectionExtensions.cs
// Description: The one registration point for DRK-1328's error-response setting — applied to both a failed
// command handler and refused validation input, with no second place a host can configure instead.

using Microsoft.Extensions.DependencyInjection;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace DKNet.AspCore.Extensions.Responses;

/// <summary>
///     Registers the one <see cref="ErrorResponseOptions" /> setting a host application uses to shape both a
///     failed command handler's response and a refused validation input's response.
/// </summary>
public static class ErrorResponseServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <see cref="ErrorResponseOptions" /> and wires it into both the FluentResults command-failure
    ///     path and the FluentValidation input-refusal path. A host calls this once; it does not also need to
    ///     configure the validation path separately.
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

        return services;
    }
}
