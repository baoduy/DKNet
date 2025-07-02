using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace SlimBus.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class FluentValidationConfig
{
    public static bool ConfigAdded { get; private set; }

    public static WebApplicationBuilder AddFluentValidationConfig(this WebApplicationBuilder builder)
    {
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssembly(typeof(AppSetup).Assembly, includeInternalTypes: true);

        ConfigAdded = true;
        return builder;
    }
}