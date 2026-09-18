using System.Runtime.CompilerServices;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>
///     Assembly-wide test host defaults (DRK-1631): disables reload-on-change on every host this assembly
///     builds via <see cref="Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder()" />, so a full run
///     stops opening an OS file-watch handle per host and stops crossing the process-wide inotify instance
///     limit part-way through the run.
/// </summary>
internal static class TestHostDefaults
{
    #region Methods

    [ModuleInitializer]
    public static void DisableConfigReloadOnChange()
    {
        const string variable = "DOTNET_hostBuilder__reloadConfigOnChange";

        if (Environment.GetEnvironmentVariable(variable) is null)
            Environment.SetEnvironmentVariable(variable, "false");
    }

    #endregion
}
