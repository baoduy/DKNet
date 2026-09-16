// Acceptance tests for DRK-1398: "created by"/"updated by" fill from a registered ICurrentUserProvider,
// independently of the tenant IDataOwnerProvider that fills "owned by". Scenarios and literal values are
// copied verbatim from DRK-1398 §5 (revision 11, frozen).

using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.DataAuthorization;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EfCore.AuditLogs.Tests;

public class CurrentUserAuditTests
{
    #region Helpers

    private static string NewDbPath() =>
        Path.Combine(Path.GetTempPath(), $"current_user_audit_{Guid.NewGuid():N}.db");

    private static async Task<(ServiceProvider Root, IServiceScope Scope, CustomerAuditDbContext Db)> BuildAsync(
        Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        register(services);
        services.AddDbContextWithHook<CustomerAuditDbContext>((_, o) =>
            o.UseSqlite($"Data Source={NewDbPath()}"));

        var root = services.BuildServiceProvider();
        var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CustomerAuditDbContext>();
        await db.Database.EnsureCreatedAsync();
        return (root, scope, db);
    }

    #endregion

    #region Tests

    [Fact]
    public async Task NewRecord_KeepsCreatingUser_AndOwningTenant()
    {
        // Given the current user is "steven.hoang@transwap.com" and the current tenant is "acme-sg"
        var (root, scope, db) = await BuildAsync(s => s
            .AddDataOwnerProvider<CustomerAuditDbContext, MutableTenantProvider>()
            .AddCurrentUserProvider<CustomerAuditDbContext, MutableCurrentUserProvider>());
        await using var _ = root;
        using var __ = scope;
        ((MutableTenantProvider)scope.ServiceProvider.GetRequiredService<IDataOwnerProvider>()).OwnershipKey =
            "acme-sg";
        ((MutableCurrentUserProvider)scope.ServiceProvider.GetRequiredService<ICurrentUserProvider>()).CurrentUser =
            "steven.hoang@transwap.com";

        // When a new customer record is saved
        var entity = new CustomerRecord { Name = "Acme Trading" };
        db.CustomerRecords.Add(entity);
        await db.SaveChangesAsync();

        // Then "created by" is "steven.hoang@transwap.com" and "owned by" is "acme-sg"
        entity.CreatedBy.ShouldBe("steven.hoang@transwap.com");
        entity.OwnedBy.ShouldBe("acme-sg");
    }

    [Fact]
    public async Task Update_RecordsChangingUser_AndPreservesCreatorAndTenant()
    {
        // Given a customer record created by "steven.hoang@transwap.com" for tenant "acme-sg"
        var (root, scope, db) = await BuildAsync(s => s
            .AddDataOwnerProvider<CustomerAuditDbContext, MutableTenantProvider>()
            .AddCurrentUserProvider<CustomerAuditDbContext, MutableCurrentUserProvider>());
        await using var _ = root;
        using var __ = scope;
        var tenantProvider = (MutableTenantProvider)scope.ServiceProvider.GetRequiredService<IDataOwnerProvider>();
        var userProvider = (MutableCurrentUserProvider)scope.ServiceProvider.GetRequiredService<ICurrentUserProvider>();
        tenantProvider.OwnershipKey = "acme-sg";
        userProvider.CurrentUser = "steven.hoang@transwap.com";

        var entity = new CustomerRecord { Name = "Acme Contact" };
        db.CustomerRecords.Add(entity);
        await db.SaveChangesAsync();

        // And the current user is "duy.nguyen@transwap.com"
        userProvider.CurrentUser = "duy.nguyen@transwap.com";

        // When the customer name is changed and saved
        entity.Rename("Acme Contact - billing@acme.com.sg");
        await db.SaveChangesAsync();

        // Then "updated by" is "duy.nguyen@transwap.com", "created by" is still "steven.hoang@transwap.com",
        // and "owned by" is still "acme-sg"
        entity.UpdatedBy.ShouldBe("duy.nguyen@transwap.com");
        entity.CreatedBy.ShouldBe("steven.hoang@transwap.com");
        entity.OwnedBy.ShouldBe("acme-sg");
    }

    [Fact]
    public async Task TenantProviderOnly_KeepsTodaysBehaviour()
    {
        // Given the application registers the tenant provider for tenant "acme-sg" and no current-user provider
        var (root, scope, db) = await BuildAsync(s => s
            .AddDataOwnerProvider<CustomerAuditDbContext, MutableTenantProvider>());
        await using var _ = root;
        using var __ = scope;
        ((MutableTenantProvider)scope.ServiceProvider.GetRequiredService<IDataOwnerProvider>()).OwnershipKey =
            "acme-sg";

        // When a new customer record is saved
        var entity = new CustomerRecord { Name = "Acme Pte Ltd" };
        db.CustomerRecords.Add(entity);
        await db.SaveChangesAsync();

        // Then "created by" is "acme-sg" and "owned by" is "acme-sg"
        entity.CreatedBy.ShouldBe("acme-sg");
        entity.OwnedBy.ShouldBe("acme-sg");
    }

    [Fact]
    public async Task CurrentUserProviderOnly_SavesWithoutOwnership()
    {
        // Given the application registers the current-user provider for "steven.hoang@transwap.com" and no
        // tenant provider
        var (root, scope, db) = await BuildAsync(s => s
            .AddCurrentUserProvider<CustomerAuditDbContext, MutableCurrentUserProvider>());
        await using var _ = root;
        using var __ = scope;
        ((MutableCurrentUserProvider)scope.ServiceProvider.GetRequiredService<ICurrentUserProvider>()).CurrentUser =
            "steven.hoang@transwap.com";

        // When a new customer record is saved
        var entity = new CustomerRecord { Name = "Acme Holdings" };
        db.CustomerRecords.Add(entity);

        // Then the save succeeds, "created by" is "steven.hoang@transwap.com", and "owned by" is empty
        await Should.NotThrowAsync(() => db.SaveChangesAsync());
        entity.CreatedBy.ShouldBe("steven.hoang@transwap.com");
        entity.OwnedBy.ShouldBeEmpty();
    }

    [Fact]
    public async Task DomainRecordedModifier_IsKept()
    {
        // Given a customer record owned by tenant "acme-sg", and the current user is "duy.nguyen@transwap.com"
        var (root, scope, db) = await BuildAsync(s => s
            .AddDataOwnerProvider<CustomerAuditDbContext, MutableTenantProvider>()
            .AddCurrentUserProvider<CustomerAuditDbContext, MutableCurrentUserProvider>());
        await using var _ = root;
        using var __ = scope;
        ((MutableTenantProvider)scope.ServiceProvider.GetRequiredService<IDataOwnerProvider>()).OwnershipKey =
            "acme-sg";
        ((MutableCurrentUserProvider)scope.ServiceProvider.GetRequiredService<ICurrentUserProvider>()).CurrentUser =
            "duy.nguyen@transwap.com";

        var entity = new CustomerRecord { Name = "Acme Merchant" };
        db.CustomerRecords.Add(entity);
        await db.SaveChangesAsync();

        // When the record is deactivated by "approver@transwap.com" and saved
        entity.Deactivate("approver@transwap.com");
        await db.SaveChangesAsync();

        // Then "updated by" is "approver@transwap.com" — kept over both the current user and the tenant key
        entity.UpdatedBy.ShouldBe("approver@transwap.com");
    }

    [Fact]
    public async Task NoProvider_SavesAndLeavesAuditFieldsUnset()
    {
        // Given the application registers no current-user provider and no tenant provider
        var (root, scope, db) = await BuildAsync(_ => { });
        await using var _ = root;
        using var __ = scope;

        // When a new customer record is saved
        var entity = new CustomerRecord { Name = "Acme Unregistered" };
        db.CustomerRecords.Add(entity);

        // Then the save succeeds and "created by", "updated by" and "owned by" are all empty
        await Should.NotThrowAsync(() => db.SaveChangesAsync());
        entity.CreatedBy.ShouldBeNullOrEmpty();
        entity.UpdatedBy.ShouldBeNullOrEmpty();
        entity.OwnedBy.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishedAuditEntry_CarriesSignedInUser()
    {
        // Given the current user is "steven.hoang@transwap.com" and the current tenant is "acme-sg"
        TestPublisher.Clear();
        var (root, scope, db) = await BuildAsync(s => s
            .AddDataOwnerProvider<CustomerAuditDbContext, MutableTenantProvider>()
            .AddCurrentUserProvider<CustomerAuditDbContext, MutableCurrentUserProvider>()
            .AddEfCoreAuditLogs<CustomerAuditDbContext, TestPublisher>());
        await using var _ = root;
        using var __ = scope;
        ((MutableTenantProvider)scope.ServiceProvider.GetRequiredService<IDataOwnerProvider>()).OwnershipKey =
            "acme-sg";
        ((MutableCurrentUserProvider)scope.ServiceProvider.GetRequiredService<ICurrentUserProvider>()).CurrentUser =
            "steven.hoang@transwap.com";

        // When a new customer record is saved
        var entity = new CustomerRecord { Name = "Acme Publishing" };
        db.CustomerRecords.Add(entity);
        await db.SaveChangesAsync();

        // Then the published audit entry shows "created by" as "steven.hoang@transwap.com"
        var published = TestPublisher.Received.SingleOrDefault(e => e.Keys.Values.Contains(entity.Id));
        published.ShouldNotBeNull();
        published.CreatedBy.ShouldBe("steven.hoang@transwap.com");
    }

    #endregion
}

#region Test doubles and fixtures

/// <summary>
///     A settable <see cref="ICurrentUserProvider" /> test double — the current user can change between
///     saves within the same scope, mirroring how a real request-scoped provider's value changes across
///     different signed-in users.
/// </summary>
internal sealed class MutableCurrentUserProvider : ICurrentUserProvider
{
    #region Properties

    public string? CurrentUser { get; set; }

    #endregion

    #region Methods

    public string? GetCurrentUser() => CurrentUser;

    #endregion
}

/// <summary>
///     A settable <see cref="IDataOwnerProvider" /> test double carrying one ownership key, used across the
///     current-user-audit scenarios so each test can pin its own literal tenant key.
/// </summary>
internal sealed class MutableTenantProvider : IDataOwnerProvider
{
    #region Properties

    public string OwnershipKey { get; set; } = string.Empty;

    #endregion

    #region Methods

    public ICollection<string> GetAccessibleKeys() =>
        string.IsNullOrEmpty(OwnershipKey) ? [] : [OwnershipKey];

    public string GetOwnershipKey() => OwnershipKey;

    #endregion
}

/// <summary>
///     A minimal audited, owned entity representing the Gherkin's "customer record" — its audit and
///     ownership fields start unset so the scenarios exercise only what the hooks stamp.
/// </summary>
internal sealed class CustomerRecord() : AuditedEntity<Guid>(Guid.NewGuid()), IOwnedBy
{
    #region Properties

    public bool IsActive { get; private set; } = true;

    public required string Name { get; set; }

    public string OwnedBy { get; private set; } = string.Empty;

    #endregion

    #region Methods

    public void Deactivate(string approvedBy, DateTimeOffset? approvedOn = null)
    {
        IsActive = false;
        SetUpdatedBy(approvedBy, approvedOn);
    }

    public void Rename(string name) => Name = name;

    #endregion
}

/// <summary>
///     A <see cref="DbContext" /> hosting <see cref="CustomerRecord" />, opted into data authorization
///     regardless of whether a scenario registers a tenant provider — <see cref="AccessibleKeys" /> falls
///     back to empty when none is registered, matching <see cref="DddContext" />'s own pattern.
/// </summary>
internal sealed class CustomerAuditDbContext(
    DbContextOptions<CustomerAuditDbContext> options,
    IEnumerable<IDataOwnerProvider> dataOwnerProviders) : DbContext(options), IDataOwnerDbContext
{
    #region Fields

    private readonly IDataOwnerProvider? _dataOwnerProvider = dataOwnerProviders.FirstOrDefault();

    #endregion

    #region Properties

    public IEnumerable<string> AccessibleKeys => _dataOwnerProvider?.GetAccessibleKeys() ?? [];

    public DbSet<CustomerRecord> CustomerRecords => Set<CustomerRecord>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<CustomerRecord>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);

            // CreatedBy is non-nullable on IAuditedProperties (an app that always stamps it never needs a
            // nullable column), but the "no provider registered at all" scenario deliberately leaves it
            // unset — so this fixture's own mapping, not the framework, allows the column to be null.
            e.Property(p => p.CreatedBy).IsRequired(false);
        });
    }

    #endregion
}

#endregion
