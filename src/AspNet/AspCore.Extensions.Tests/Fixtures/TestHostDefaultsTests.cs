using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>
///     Guards the default test host builder against the config-watcher leak (DRK-1631): a full run of this
///     assembly builds ~85 hosts via <see cref="WebApplication.CreateBuilder()" />, and each JSON configuration
///     source left with reload-on-change enabled opens an OS file-watch handle, crossing the process-wide
///     inotify instance limit part-way through the run.
/// </summary>
public class TestHostDefaultsTests
{
    #region Methods

    [Fact]
    public void CreateBuilder_Always_RegistersJsonSourcesWithReloadOnChangeDisabled()
    {
        var builder = WebApplication.CreateBuilder();

        var jsonSources = builder.Configuration.Sources.OfType<JsonConfigurationSource>().ToList();

        jsonSources.ShouldNotBeEmpty();
        jsonSources.ShouldAllBe(source => source.ReloadOnChange == false);
    }

    #endregion
}
