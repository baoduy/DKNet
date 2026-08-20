// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ContextualSourceOpenApiTransformers.cs
// Description: Removes IContextualSource-declared request members from the published OpenAPI description.

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DKNet.AspCore.Extensions;

/// <summary>
///     Removes JSON-body-bound properties declared via <see cref="IContextualSource" /> (e.g.
///     <see cref="FromClaimAttribute" />) from the generated OpenAPI schema — they are populated by the host,
///     never supplied by the caller, so they are not advertised as caller input.
/// </summary>
internal sealed class ContextualSourceSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema.Properties is not null)
            foreach (var property in context.JsonTypeInfo.Properties)
                if (property.AttributeProvider?.GetCustomAttributes(true).Any(a => a is IContextualSource) == true)
                    schema.Properties.Remove(property.Name);

        return Task.CompletedTask;
    }
}

/// <summary>
///     Removes <c>[AsParameters]</c>/query-bound properties declared via <see cref="IContextualSource" /> from the
///     generated OpenAPI operation's parameter list — <see cref="ContextualSourceSchemaTransformer" /> only
///     covers JSON body binding, where the declared property is part of a body schema rather than its own
///     operation parameter.
/// </summary>
internal sealed class ContextualSourceOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is null) return Task.CompletedTask;

        var declaredNames = context.Description.ParameterDescriptions
            .Where(p => p.ModelMetadata.ContainerType?.GetProperty(p.ModelMetadata.PropertyName ?? string.Empty)
                    is { } property
                && property.GetCustomAttributes(true).Any(a => a is IContextualSource))
            .Select(p => p.Name)
            .ToHashSet();

        if (declaredNames.Count > 0)
            foreach (var parameter in operation.Parameters.Where(p => p.Name is not null && declaredNames.Contains(p.Name)).ToList())
                operation.Parameters.Remove(parameter);

        return Task.CompletedTask;
    }
}
