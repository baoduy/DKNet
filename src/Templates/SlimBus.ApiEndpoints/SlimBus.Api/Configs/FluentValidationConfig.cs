using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace SlimBus.Api.Configs;

[ExcludeFromCodeCoverage]
internal static class FluentValidationConfig
{
    #region Properties

    public static bool ConfigAdded { get; private set; }

    #endregion

    #region Methods

    public static WebApplicationBuilder AddFluentValidationConfig(this WebApplicationBuilder builder)
    {
        builder.Services.AddFluentValidationAutoValidation();
        builder.Services.AddValidatorsFromAssembly(typeof(AppSetup).Assembly, includeInternalTypes: true);

        ConfigAdded = true;
        return builder;
    }

    #endregion
}