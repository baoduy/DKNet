using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using Microsoft.IdentityModel.Tokens;

namespace SlimBus.Api.Configs.Auth;

/// <summary>
///     Extends JwtSecurityTokenHandler to validate JWT tokens using the Microsoft Graph API.
/// </summary>
[ExcludeFromCodeCoverage]
internal class MsGraphJwtTokenHandler : JwtSecurityTokenHandler
{
    #region Methods

    /// <summary>
    ///     Override the default signature validation to bypass internal signature validation.
    ///     This reads the token without validating the signature, assuming external validation.
    /// </summary>
    /// <param name="token">The JWT token to read.</param>
    /// <param name="validationParameters">The token validation parameters.</param>
    /// <returns>The JWT security token read from the provided token string.</returns>
    protected override JwtSecurityToken
        ValidateSignature(string token, TokenValidationParameters validationParameters) =>
        this.ReadJwtToken(token);

    /// <summary>
    ///     Asynchronously validates the JWT token.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <param name="validationParameters">The token validation parameters.</param>
    /// <returns>A Task containing the TokenValidationResult.</returns>
    public override async Task<TokenValidationResult> ValidateTokenAsync(
        string token,
        TokenValidationParameters validationParameters)
    {
        // Override the default signature validation process with the custom method.
        validationParameters.SignatureValidator = this.ValidateSignature;

        // Disable issuer signing key validation.
        validationParameters.ValidateIssuerSigningKey = false;

        // Perform base class token validation (ignores signature due to the above settings).
        var rs = await base.ValidateTokenAsync(token, validationParameters);

        // If base validation fails, return the failed result immediately.
        if (!rs.IsValid)
        {
            return rs;
        }

        // Further validate the token by checking it against Microsoft Graph API.
        try
        {
            await ValidateTokenWithMsGraphEndPoint(token);

            // Return the successful result if Microsoft Graph validation passes.
            return rs;
        }
        catch (SecurityTokenValidationException ex)
        {
            // Return a failed result if there was an exception during Microsoft Graph validation.
            return new TokenValidationResult
            {
                IsValid = false,
                Exception = ex
            };
        }
    }

    /// <summary>
    ///     Validates the provided JWT token by calling the Microsoft Graph API.
    /// </summary>
    /// <param name="token">The JWT token to validate.</param>
    /// <exception cref="SecurityTokenValidationException">Thrown if the token validation against Microsoft Graph fails.</exception>
    private static async Task ValidateTokenWithMsGraphEndPoint(string token)
    {
        // Create an HTTP client for sending requests.
        using var httpClient = new HttpClient();

        // Add the token to the Authorization header as a Bearer token.
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Send a GET request to the Microsoft Graph API to validate the token.
        var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");

        // If the response is not successful, throw an exception indicating token validation failure.
        if (!response.IsSuccessStatusCode)
        {
            throw new SecurityTokenValidationException("Failed to validate token against Microsoft Graph.");
        }
    }

    #endregion
}