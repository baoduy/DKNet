using System;
using System.Collections.Generic;
using System.Linq;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Hooks;
using DKNet.EfCore.Specifications;
using DKNet.SlimBus.Extensions;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SlimBus.Generators.Tests.Api.Crud;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;
using Xunit;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     DRK-1355 §5 scenarios 6, 7 and 8 (@unit — "no HTTP host, not no generated code" per §7 slice notes):
///     registration-time behaviour of by-name exclusion, driven against the real generated
///     <c>MapGadgetCrud</c> extension without ever calling <c>StartAsync</c> or dispatching HTTP. Scenarios 6
///     and 8 enumerate <c>IEndpointRouteBuilder.DataSources[*].Endpoints</c> to count published routes, which
///     forces <c>RequestDelegateFactory</c> to compile each route's metadata — unlike
///     <see cref="PerRouteConfigurationRegistrationTests" />'s bare, unconfigured <see cref="WebApplication" />,
///     this needs the same DI registrations (mapper, repository, message bus) <see cref="GadgetTestHost" /> uses,
///     minus <c>UseTestServer</c>/<c>StartAsync</c>.
/// </summary>
public class PerMemberExclusionRegistrationTests
{
    private static (WebApplication App, SqliteConnection Connection) BuildAppWithoutHost()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
        builder.Services.AddDbContextWithHook<GadgetDbContext>((_, o) => o.UseSqlite(connection));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<GadgetDbContext>());
        builder.Services.AddSpecRepo<GadgetDbContext>();
        builder.Services
            .AddSlimBusEfCoreInterceptor<GadgetDbContext>()
            .AddSlimMessageBus(mbb => mbb
                .AddJsonSerializer()
                .AddServicesFromAssembly(typeof(GadgetTestHost).Assembly)
                .AddChildBus(
                    "Memory",
                    mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(GadgetTestHost).Assembly)));

        return (builder.Build(), connection);
    }

    private static int CountEndpoints(IEndpointRouteBuilder group) =>
        group.DataSources.SelectMany(ds => ds.Endpoints).Count();

    private static HashSet<string> RouteSignatures(IEndpointRouteBuilder group) =>
        group.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => $"{e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Single()} {e.RoutePattern.RawText}")
            .ToHashSet();

    [Fact]
    public void MapGadgetCrud_ExcludingTwoNamesInEitherOrder_PublishesTheSameSixRoutes()
    {
        // R5: exclusion is a set — call order must not change the published surface. Comparing the route
        // SETS (pattern + HTTP method), not just their counts, is the point of the scenario: two different
        // sets of six would satisfy a count-only assertion.
        var (appA, connectionA) = BuildAppWithoutHost();
        using var _1 = connectionA;
        var groupA = appA.MapGroup("/gadgets");
        groupA.MapGadgetCrud(o => o.Exclude("Rename").Exclude("Discontinue"));

        var (appB, connectionB) = BuildAppWithoutHost();
        using var _2 = connectionB;
        var groupB = appB.MapGroup("/gadgets");
        groupB.MapGadgetCrud(o => o.Exclude("Discontinue").Exclude("Rename"));

        var routesA = RouteSignatures(groupA);
        var routesB = RouteSignatures(groupB);

        routesA.Count.ShouldBe(6);
        routesB.ShouldBe(routesA);
    }

    [Fact]
    public void MapGadgetCrud_ExcludingAnUnmatchedMemberName_ReportsAnErrorNamingItAndPublishesNoRoute()
    {
        // "Discontinue" IS a real Gadget action (§7 slice note), so the deliberately-unmatched name is
        // "Discontinued" — one letter added, matching no generated route. No DI needed: the throw must
        // happen before any route reaches registration, so no endpoint is ever compiled.
        var app = WebApplication.CreateBuilder().Build();
        var group = app.MapGroup("/gadgets-unmatched");

        var exception = Should.Throw<ArgumentException>(() =>
            group.MapGadgetCrud(o => o.Exclude("Discontinued")));

        exception.Message.ShouldContain("Discontinued");

        // R3: the throw happens before any route is registered — no partial registration.
        ((IEndpointRouteBuilder)group).DataSources.ShouldBeEmpty();
    }

    [Fact]
    public void MapGadgetCrud_WithNoExclusion_PublishesAllEightRoutes()
    {
        // R6: nothing is excluded by default.
        var (app, connection) = BuildAppWithoutHost();
        using var _ = connection;
        var group = app.MapGroup("/gadgets-default");

        group.MapGadgetCrud();

        CountEndpoints(group).ShouldBe(8);
    }
}
