// DRK-2362 §5 acceptance tests — the request-backed sensitive-data principal accessor (DRK-2375). Every scenario
// fails today: SensitiveDataPrincipalAccessorConfig is a stub @ the acceptance-tests commit, so
// AddSensitiveDataPrincipalAccessor throws NotImplementedException. The host opts its JSON options into
// UseRoleAwareSensitiveData itself, with the accessor the package registered — that step stays the host's.

using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Security;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Extensions.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Security;

public class SensitiveDataPrincipalAccessorTests
{
    #region Methods

    private static readonly AccountDto Account = new("ACC-1001", "Treasury operating account", "TX-77-0042");

    // Scenario Outline: Sensitive data follows the signed-in user of the request
    [Theory]
    [InlineData("\"treasury-ops\" with the role finance", "shown")]
    [InlineData("\"support-desk\" without the role", "withheld")]
    [InlineData("an anonymous caller", "withheld")]
    public async Task AddSensitiveDataPrincipalAccessor_CallerRequestsAccount_TaxNumberShownOnlyToFinance(
        string caller, string shown)
    {
        await using var host = await HostingTestHost.StartAsync(
            b =>
            {
                BearerTestAuthHandler.Register(b.Services);
                b.Services.AddSensitiveDataPrincipalAccessor();
                b.Services.AddOptions<JsonOptions>()
                    .Configure<ISensitiveDataPrincipalAccessor>((o, accessor) =>
                        o.SerializerOptions.UseRoleAwareSensitiveData(accessor));
            },
            app =>
            {
                app.UseAuthentication();
                app.MapGet("/v1/accounts/{id}", (string id) => id == Account.Id ? Results.Ok(Account) : Results.NotFound());
            });
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/accounts/ACC-1001");
        switch (caller)
        {
            case "\"treasury-ops\" with the role finance":
                request.Headers.Add("Authorization", "Bearer treasury-ops");
                request.Headers.Add(BearerTestAuthHandler.RolesHeader, "finance");
                break;
            case "\"support-desk\" without the role":
                request.Headers.Add("Authorization", "Bearer support-desk");
                request.Headers.Add(BearerTestAuthHandler.RolesHeader, "support");
                break;
        }

        using var response = await client.SendAsync(request);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("id").GetString().ShouldBe("ACC-1001");
        body.GetProperty("name").GetString().ShouldBe("Treasury operating account");
        if (shown == "shown")
            body.GetProperty("taxNumber").GetString().ShouldBe("TX-77-0042");
        else
            body.TryGetProperty("taxNumber", out _).ShouldBeFalse();
    }

    // Scenario: Outside a request there is no signed-in user
    [Fact]
    public void AddSensitiveDataPrincipalAccessor_NoRequestRunning_BackgroundSerializationWithholdsTaxNumber()
    {
        var services = new ServiceCollection();
        services.AddSensitiveDataPrincipalAccessor();
        using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<ISensitiveDataPrincipalAccessor>();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web).UseRoleAwareSensitiveData(accessor);

        var json = JsonSerializer.Serialize(Account, options);

        accessor.Current.ShouldBeNull();
        json.ShouldBe("""{"id":"ACC-1001","name":"Treasury operating account"}""");
    }

    #endregion

    /// <summary>An account whose tax number only callers with the role "finance" may see.</summary>
    public sealed record AccountDto(string Id, string Name, [property: SensitiveData("finance")] string TaxNumber);
}
