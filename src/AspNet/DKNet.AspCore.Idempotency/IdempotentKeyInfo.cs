using System.Text.RegularExpressions;
using FluentResults;

namespace DKNet.AspCore.Idempotency;

/// <summary>
///     Represents the idempotency key information extracted from an HTTP request.
///     This record encapsulates the idempotency key along with the endpoint and HTTP method
///     to uniquely identify requests across different operations.
/// </summary>
/// <remarks>
///     This type is used internally by <see cref="IdempotencyEndpointFilter" /> to extract and validate
///     idempotency information from incoming requests. The combination of idempotency key, endpoint route,
///     and HTTP method creates a composite key that ensures the same idempotency key can be safely reused
///     across different endpoints or HTTP verbs without conflicts.
/// </remarks>
public sealed record IdempotentKeyInfo
{
    /// <summary>
    ///     Gets or initializes the idempotency key extracted from the request header.
    /// </summary>
    /// <value>
    ///     The idempotency key value from the configured header (default: X-Idempotency-Key),
    ///     or <c>null</c> if the header was not present in the request.
    /// </value>
    public string? IdempotentKey { get; init; }

    /// <summary>
    ///     Gets or initializes the endpoint route template or path from the HTTP request.
    /// </summary>
    /// <value>
    ///     The route template from endpoint metadata (e.g., "/api/orders/{id}"), or the actual
    ///     request path if route metadata is unavailable. This value is required and must not be null.
    /// </value>
    /// <remarks>
    ///     For minimal APIs, the route template is extracted from
    ///     <see cref="Microsoft.AspNetCore.Components.RouteAttribute" /> metadata.
    ///     If unavailable, falls back to <see cref="Microsoft.AspNetCore.Http.HttpRequest.Path" />.
    /// </remarks>
    public required string Endpoint { get; init; }

    /// <summary>
    ///     Gets or initializes the HTTP method (verb) of the request.
    /// </summary>
    /// <value>
    ///     The HTTP method in uppercase format (e.g., "GET", "POST", "PUT", "DELETE").
    ///     This value is required and must not be null.
    /// </value>
    /// <remarks>
    ///     The HTTP method is normalized to uppercase to ensure consistent composite key generation
    ///     regardless of the original request casing.
    /// </remarks>
    public required string Method { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the idempotency key is valid and can be used for request processing.
    /// </summary>
    /// <value>
    ///     <c>true</c> if the <see cref="IdempotentKey" /> is not null, empty, or whitespace; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    ///     This property performs a basic validation check. Additional validation (format, length, pattern)
    ///     is performed by <see cref="IdempotencyEndpointFilter" /> before processing the request.
    /// </remarks>
    public IResultBase IsValid(IdempotencyOptions options)
    {
        // Validate header presence
        if (string.IsNullOrWhiteSpace(IdempotentKey))
            return Result.Fail(
                $"The '{options.IdempotencyHeaderKey}' header is required for idempotent requests.");

        // Validate key length
        if (IdempotentKey.Length > options.MaxIdempotencyKeyLength)
            return Result.Fail(
                $"Idempotency key must not exceed {options.MaxIdempotencyKeyLength} characters.");

        // Validate key format
        if (!Regex.IsMatch(IdempotentKey, options.IdempotencyKeyPattern))
            return Result.Fail(
                "Idempotency key format is invalid. Allowed characters: alphanumeric, hyphens, underscores.");

        return Result.Ok();
    }

    /// <summary>
    ///     Gets the composite key combining the HTTP method, endpoint route, and idempotency key
    ///     to create a unique identifier for the idempotent request.
    /// </summary>
    /// <value>
    ///     A composite string in the format "METHOD:ENDPOINT:KEY" (e.g., "POST:/api/orders:abc-123-def").
    ///     This composite key ensures that the same idempotency key can be safely reused across
    ///     different endpoints or HTTP methods without conflicts.
    /// </value>
    /// <remarks>
    ///     <para>
    ///         The composite key is used as the unique identifier when storing and retrieving idempotent
    ///         request results from the underlying storage mechanism (e.g., SQL Server, Redis).
    ///     </para>
    ///     <para>
    ///         By combining the HTTP method and endpoint with the idempotency key, the system allows:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>The same idempotency key to be used for different endpoints simultaneously.</description>
    ///         </item>
    ///         <item>
    ///             <description>The same idempotency key to be used for different HTTP methods on the same endpoint.</description>
    ///         </item>
    ///         <item>
    ///             <description>Isolation of idempotent operations across different API operations.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    public string CompositeKey => $"{Method}:{Endpoint}:{IdempotentKey ?? string.Empty}";
}