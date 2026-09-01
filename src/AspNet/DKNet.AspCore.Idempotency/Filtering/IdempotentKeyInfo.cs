using System.Text;
using System.Text.RegularExpressions;
using FluentResults;

namespace DKNet.AspCore.Idempotency.Filtering;

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
    ///     Gets a log/display-safe projection of <see cref="IdempotentKey" /> with all line-breaking and
    ///     other control characters removed.
    /// </summary>
    /// <value>
    ///     <see cref="IdempotentKey" /> with CR, LF, U+2028, U+2029, and every other C0/C1 control character
    ///     stripped, or <see cref="string.Empty" /> when <see cref="IdempotentKey" /> is <c>null</c>.
    /// </value>
    /// <remarks>
    ///     This projection is for logging and client-facing display only — it must never be used to compute
    ///     <see cref="CompositeKey" /> or for storage/lookup, both of which continue to use the raw
    ///     <see cref="IdempotentKey" />.
    /// </remarks>
    public string SafeKey
    {
        get
        {
            if (string.IsNullOrEmpty(IdempotentKey)) return string.Empty;

            var noNewLines = IdempotentKey
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal);

            var builder = new StringBuilder(noNewLines.Length);
            foreach (var c in noNewLines)
            {
                if (char.IsControl(c) || c == '\u2028' || c == '\u2029') continue;
                builder.Append(c);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    ///     Gets or initializes the endpoint route template or path from the HTTP request.
    /// </summary>
    /// <value>
    ///     The route template from endpoint metadata (e.g., "/API/ORDERS/{ID}"), or the actual
    ///     request path if route metadata is unavailable. This value is required, must not be null,
    ///     and is normalized to upper-invariant casing so requests differing only in path casing
    ///     resolve to the same idempotency scope.
    /// </value>
    /// <remarks>
    ///     The route template is resolved, in order, from <see cref="Microsoft.AspNetCore.Routing.RouteEndpoint" />'s
    ///     <c>RoutePattern.RawText</c>, then <see cref="Microsoft.AspNetCore.Http.Metadata.IRouteDiagnosticsMetadata" />'s
    ///     <c>Route</c>, then falls back to <see cref="Microsoft.AspNetCore.Http.HttpRequest.Path" />. The resolved
    ///     value is upper-invariant-cased before assignment.
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
    ///     Gets or initializes the caller scope used to isolate idempotency keys between different principals.
    /// </summary>
    /// <value>
    ///     A scope string such as "user:{id}", "auth:{hmac}", "ip:{address}", or <see cref="string.Empty" /> for
    ///     anonymous, unscoped callers. The default is <see cref="string.Empty" />.
    /// </value>
    /// <remarks>
    ///     The scope is prepended to the composite key so that two different callers sending the same
    ///     idempotency key to the same endpoint do not share a cache slot.
    /// </remarks>
    public string Scope { get; init; } = string.Empty;

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
    ///     Gets the composite key combining the caller scope, HTTP method, endpoint route, and idempotency key
    ///     to create a unique identifier for the idempotent request.
    /// </summary>
    /// <value>
    ///     A composite string in the format "SCOPE:METHOD:ENDPOINT:KEY" (e.g., "user:42:POST:/api/orders:abc-123-def").
    ///     This composite key ensures that the same idempotency key can be safely reused across
    ///     different callers, endpoints, or HTTP methods without conflicts.
    /// </value>
    /// <remarks>
    ///     <para>
    ///         The composite key is used as the unique identifier when storing and retrieving idempotent
    ///         request results from the underlying storage mechanism (e.g., SQL Server, Redis).
    ///     </para>
    ///     <para>
    ///         By combining the caller scope with the HTTP method and endpoint, the system allows:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>The same idempotency key to be used by different callers without collision.</description>
    ///         </item>
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
    public string CompositeKey => $"{Scope}:{Method}:{Endpoint}:{IdempotentKey ?? string.Empty}";

    /// <summary>
    ///     Returns a log/display-safe representation of this instance.
    /// </summary>
    /// <returns>
    ///     A string containing <see cref="SafeKey" />, <see cref="Method" />, and <see cref="Endpoint" />.
    ///     Never includes <see cref="Scope" /> or the raw <see cref="IdempotentKey" />.
    /// </returns>
    public override string ToString() => $"Key={SafeKey}, Method={Method}, Endpoint={Endpoint}";
}