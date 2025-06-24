using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace SlimBus.Api.Configs.RateLimits;

/// <summary>
/// Provides rate limiting policies based on client IP or JWT user identity
/// </summary>
internal class RateLimitPolicyProvider
{
    private readonly RateLimitOptions _options;
    private readonly ILogger<RateLimitPolicyProvider> _logger;

    public RateLimitPolicyProvider(IOptions<RateLimitOptions> options, ILogger<RateLimitPolicyProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets the partition key for rate limiting based on authorization header or IP address
    /// </summary>
    public string GetPartitionKey(HttpContext context)
    {
        // Try to get user identity from JWT token first
        var userIdentity = GetUserIdentityFromJwt(context);
        if (!string.IsNullOrEmpty(userIdentity))
        {
            _logger.LogDebug("Using JWT user identity for rate limiting: {UserIdentity}", userIdentity);
            return $"user:{userIdentity}";
        }

        // Fall back to client IP address
        var clientIp = GetClientIpAddress(context);
        _logger.LogDebug("Using client IP for rate limiting: {ClientIp}", clientIp);
        return $"ip:{clientIp}";
    }

    /// <summary>
    /// Extracts user identity from JWT token in authorization header
    /// </summary>
    private string? GetUserIdentityFromJwt(HttpContext context)
    {
        try
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var token = authHeader["Bearer ".Length..].Trim();
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            var jwtHandler = new JwtSecurityTokenHandler();
            if (!jwtHandler.CanReadToken(token))
            {
                return null;
            }

            var jwtToken = jwtHandler.ReadJwtToken(token);
            
            // Try to get user identity in order of preference: name, email, sub (ID)
            var userIdentity = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value
                            ?? jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                            ?? jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                            ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                            ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "name")?.Value
                            ?? jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

            return userIdentity;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract user identity from JWT token");
            return null;
        }
    }

    /// <summary>
    /// Gets the client IP address from the HTTP context
    /// </summary>
    private string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded headers first (in case of proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var firstIp = forwardedFor.Split(',')[0].Trim();
            if (IPAddress.TryParse(firstIp, out _))
            {
                return firstIp;
            }
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out _))
        {
            return realIp;
        }

        // Fall back to connection remote IP address
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}