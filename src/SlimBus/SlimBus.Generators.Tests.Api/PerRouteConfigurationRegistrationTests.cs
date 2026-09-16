using DKNet.AspCore.Extensions.Endpoints;
using Microsoft.AspNetCore.Builder;
using Shouldly;
using SlimBus.Generators.Tests.Api.Crud;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     DRK-1327 §5 scenario 4 (@unit): "A misspelt route name is reported, not ignored." Drives the real
///     generated <c>MapGadgetCrud</c> extension directly against a bare <see cref="WebApplication" /> — a
///     registration-time behaviour, so no HTTP host or fixture is needed.
/// </summary>
public class PerRouteConfigurationRegistrationTests
{
    [Fact]
    public void MapGadgetCrud_WithMisspeltRouteName_ReportsAnErrorNamingIt()
    {
        // Gadget publishes update routes "UpdatePrice" and "Rename" (DRK-1327 §5 slice mapping); "UpdatePrise"
        // is the deliberate typo of "UpdatePrice", mirroring the spec's own "ChangePrise" typo of "ChangePrice".
        var app = WebApplication.CreateBuilder().Build();

        var exception = Should.Throw<ArgumentException>(() =>
            app.MapGroup("/gadgets-typo")
                .MapGadgetCrud(o => o.Configure("UpdatePrise", b => b.RequireAuthorization("product.price"))));

        exception.Message.ShouldContain("UpdatePrise");
    }
}
