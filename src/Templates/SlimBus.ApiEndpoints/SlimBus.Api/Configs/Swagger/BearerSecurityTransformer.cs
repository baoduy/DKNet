using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace SlimBus.Api.Configs.Swagger;

[ExcludeFromCodeCoverage]
internal sealed class BearerSecurityTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (authenticationSchemes.Any(authScheme => string.Equals(authScheme.Name,
                JwtBearerDefaults.AuthenticationScheme, StringComparison.OrdinalIgnoreCase)))
        {
            var securityScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = securityScheme;

            var securityRequirement = new OpenApiSecurityRequirement
            {
                { securityScheme, [] }
            };

            foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations.Values))
                operation.Security.Add(securityRequirement);
        }
    }
}