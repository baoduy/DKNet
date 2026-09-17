// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ContextualSourceOpenApiTransformers.cs
// Description: Removes IContextualSource-declared request members from the published OpenAPI description.

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DKNet.AspCore.Extensions.ModelBinding;

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
///     operation parameter. Also advertises every <see cref="FromRequestHeaderAttribute" />-declared member,
///     body-bound or not, as its own <c>in: header</c> parameter — the caller-facing contract for a source the
///     caller is expected to actually send, unlike a claim which is never advertised at all.
/// </summary>
internal sealed class ContextualSourceOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Parameters is not null)
        {
            var declaredNames = context.Description.ParameterDescriptions
                .Where(p => p.ModelMetadata.ContainerType?.GetProperty(p.ModelMetadata.PropertyName ?? string.Empty)
                        is { } property
                    && property.GetCustomAttributes(true).Any(a => a is IContextualSource))
                .Select(p => p.Name)
                .ToHashSet();

            if (declaredNames.Count > 0)
                foreach (var parameter in operation.Parameters.Where(p => p.Name is not null && declaredNames.Contains(p.Name)).ToList())
                    operation.Parameters.Remove(parameter);
        }

        var existingParameterNames = (operation.Parameters ?? [])
            .Where(p => p.Name is not null)
            .Select(p => p.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var headers = context.Description.ParameterDescriptions
            .Select(p => p.ModelMetadata.ContainerType ?? p.ModelMetadata.ModelType)
            .Distinct()
            .SelectMany(ContextualMemberScanner.GetDeclaredMembers)
            .Select(m => m.Source)
            .OfType<FromRequestHeaderAttribute>();

        foreach (var header in headers.Where(h => existingParameterNames.Add(h.HeaderName)))
            (operation.Parameters ??= []).Add(new OpenApiParameter
            {
                Name = header.HeaderName,
                In = ParameterLocation.Header,
                Required = false,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String }
            });

        return Task.CompletedTask;
    }
}
