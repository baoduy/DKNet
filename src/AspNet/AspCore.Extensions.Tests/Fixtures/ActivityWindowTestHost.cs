using AspCore.Extensions.Tests.TestEntities;
using DKNet.AspCore.Extensions.Endpoints;
using DKNet.EfCore.Specifications;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>
///     Builds a throwaway <c>MapGetList</c> host for the default recent-activity window (DRK-1164 §5), mapping
///     <see cref="WidgetEntity" /> (no audit timestamps), <see cref="OrderEntity" /> (audited via
///     <see cref="DKNet.EfCore.Abstractions.Entities.AuditedEntity" />) and <see cref="InvoiceEntity" /> (audited
///     via <see cref="DKNet.EfCore.Abstractions.Entities.IAuditedProperties" /> directly). Every scenario seeds
///     its own rows at instants relative to <see cref="DateTimeOffset.UtcNow" />, so each test builds its own
///     host rather than sharing seeded state through an <c>IClassFixture</c>.
/// </summary>
public static class ActivityWindowTestHost
{
    #region Methods

    /// <summary>Builds, seeds and starts a host; caller owns disposing the returned <see cref="WebApplication" />.</summary>
    /// <param name="seed">Adds rows to the in-memory <see cref="WidgetDbContext" /> before the host starts.</param>
    /// <param name="configureBuilder">Applies scenario-specific DI/configuration before the host builds.</param>
    public static async Task<WebApplication> BuildAsync(
        Action<WidgetDbContext> seed,
        Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<IMapper>(new Mapper(new TypeAdapterConfig()));
        builder.Services.AddOpenApi();
        var dbName = Guid.NewGuid().ToString("N");
        builder.Services.AddDbContext<WidgetDbContext>(o => o.UseInMemoryDatabase(dbName));
        builder.Services.AddScoped<DbContext>(p => p.GetRequiredService<WidgetDbContext>());
        builder.Services.AddSpecRepo<WidgetDbContext>();
        configureBuilder?.Invoke(builder);

        var app = builder.Build();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WidgetDbContext>();
            seed(db);
            await db.SaveChangesAsync();
        }

        var group = app.MapGroup("/p");
        group.MapGetList<WidgetEntity, WidgetModel>("/widgets");
        group.MapGetList<OrderEntity, OrderModel>("/orders");
        group.MapGetList<InvoiceEntity, InvoiceModel>("/invoices");
        app.MapOpenApi();

        await app.StartAsync();
        return app;
    }

    #endregion
}
