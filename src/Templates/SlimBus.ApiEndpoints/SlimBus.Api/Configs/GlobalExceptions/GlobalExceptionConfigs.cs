namespace SlimBus.Api.Configs.GlobalExceptions;

[ExcludeFromCodeCoverage]
internal static class GlobalExceptionConfigs
{
    #region Fields

    // A flag to check if the global exception configuration has been added
    private static bool _configAdded;

    #endregion

    #region Methods

    /// <summary>
    ///     Configures the services to add global exception handling.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection with the global exception handler configured.</returns>
    public static IServiceCollection AddGlobalException(this IServiceCollection services)
    {
        // Configure Problem Details middleware to customize the response for exceptions
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                // Set the instance to the request method and path
                ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";

                // Add the trace identifier to the problem details extensions
                ctx.ProblemDetails.Extensions.Add("trace-id", ctx.HttpContext.TraceIdentifier);
            };
        });

        // Add the global exception handler middleware
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // Set the flag to indicate that the configuration has been added
        _configAdded = true;

        // Return the service collection
        return services;
    }

    /// <summary>
    ///     Applies the global exception handling middleware to the application.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The web application with the global exception handler applied.</returns>
    public static WebApplication UseGlobalException(this WebApplication app)
    {
        // Check if the global exception configuration has been added
        if (!_configAdded)
        {
            return app;
        }

        // Use the exception handler middleware
        app.UseExceptionHandler();

        // Log a message to indicate that the global exception handler is enabled
        Console.WriteLine("Global Exception enabled.");

        // Return the web application
        return app;
    }

    #endregion
}