// Unit tests (inner loop) for the two "already registered" guard branches in
// EfCoreAuditLogSetup.AddCurrentUserProvider that no DRK-1398 acceptance scenario exercises: every AT
// registers ICurrentUserProvider and AuditLogOptions for the first time, so the "no-op if already
// registered" halves of §3 row 4's contract need their own coverage.

using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.DataAuthorization;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace EfCore.AuditLogs.Tests;

public class EfCoreAuditLogSetupTests
{
    #region Tests

    [Fact]
    public void AddCurrentUserProvider_WhenProviderAlreadyRegistered_DoesNotRegisterAnother()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICurrentUserProvider, FirstCurrentUserProvider>();

        services.AddCurrentUserProvider<SetupTestDbContext, SecondCurrentUserProvider>();

        var provider = services.BuildServiceProvider();
        provider.GetServices<ICurrentUserProvider>().ShouldHaveSingleItem();
        provider.GetRequiredService<ICurrentUserProvider>().GetCurrentUser().ShouldBe("first");
    }

    [Fact]
    public void AddCurrentUserProvider_WhenAuditLogOptionsAlreadyRegistered_DoesNotOverwriteThem()
    {
        var services = new ServiceCollection();
        services.AddEfCoreAuditHook<SetupTestDbContext>(AuditLogBehaviour.OnlyAttributedAuditedEntities);

        services.AddCurrentUserProvider<SetupTestDbContext, FirstCurrentUserProvider>();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<AuditLogOptions>>().Value.Behaviour
            .ShouldBe(AuditLogBehaviour.OnlyAttributedAuditedEntities);
    }

    [Fact]
    public async Task RegisteredProvider_ReturningEmpty_LeavesCreatedByUnset()
    {
        // A registered ICurrentUserProvider whose GetCurrentUser() returns empty must not stamp
        // CreatedBy — none of the frozen DRK-1398 scenarios register a provider that returns empty,
        // so EfCoreAuditHook's own "skip when null/empty" guard needs its own test.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCurrentUserProvider<CustomerAuditDbContext, MutableCurrentUserProvider>();
        services.AddDbContextWithHook<CustomerAuditDbContext>((_, o) =>
            o.UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"guard_{Guid.NewGuid():N}.db")}"));

        await using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerAuditDbContext>();
        await db.Database.EnsureCreatedAsync();
        ((MutableCurrentUserProvider)scope.ServiceProvider.GetRequiredService<ICurrentUserProvider>()).CurrentUser =
            string.Empty;

        var entity = new CustomerRecord { Name = "Acme Empty User" };
        db.CustomerRecords.Add(entity);
        await db.SaveChangesAsync();

        // CreatedOn pins that the guard skipped stamping entirely, rather than stamping an empty
        // CreatedBy — an empty CreatedBy alone can't tell "never touched" from "touched with empty".
        entity.CreatedBy.ShouldBeNullOrEmpty();
        entity.CreatedOn.ShouldBe(default);
    }

    [Fact]
    public async Task RegisteredCurrentUserProvider_ReturningEmpty_FallsBackToOwnershipKey()
    {
        // N2 (PR #458 POLISH round on DRK-1401): with BOTH a tenant provider and a current-user
        // provider registered, an empty GetCurrentUser() must still let DataOwnerHook fall back to the
        // ownership key for CreatedBy — pins that DataOwnerHook.cs:62 keys off the provider's *value*,
        // not merely whether one is registered (a `currentUserProvider is null` rewrite would also
        // pass every other test in this suite, since none pairs a registered-but-empty provider with a
        // tenant provider).
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataOwnerProvider<CustomerAuditDbContext, MutableTenantProvider>();
        services.AddCurrentUserProvider<CustomerAuditDbContext, MutableCurrentUserProvider>();
        services.AddDbContextWithHook<CustomerAuditDbContext>((_, o) =>
            o.UseSqlite($"Data Source={Path.Combine(Path.GetTempPath(), $"n2_{Guid.NewGuid():N}.db")}"));

        await using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerAuditDbContext>();
        await db.Database.EnsureCreatedAsync();
        ((MutableTenantProvider)scope.ServiceProvider.GetRequiredService<IDataOwnerProvider>()).OwnershipKey =
            "acme-sg";
        ((MutableCurrentUserProvider)scope.ServiceProvider.GetRequiredService<ICurrentUserProvider>()).CurrentUser =
            string.Empty;

        var entity = new CustomerRecord { Name = "Acme N2" };
        db.CustomerRecords.Add(entity);
        await db.SaveChangesAsync();

        entity.CreatedBy.ShouldBe("acme-sg");
        entity.OwnedBy.ShouldBe("acme-sg");
    }

    #endregion

    #region Test doubles

    private sealed class SetupTestDbContext(DbContextOptions<SetupTestDbContext> options) : DbContext(options);

    private sealed class FirstCurrentUserProvider : ICurrentUserProvider
    {
        public string? GetCurrentUser() => "first";
    }

    private sealed class SecondCurrentUserProvider : ICurrentUserProvider
    {
        public string? GetCurrentUser() => "second";
    }

    #endregion
}
