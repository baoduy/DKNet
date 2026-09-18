// New tests to cover [Encrypted]-attributed audit redaction (DRK-1569 / LOG-002 / SEC-005).

using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.AuditLogs.Internals;
using DKNet.EfCore.Encryption.Attributes;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace EfCore.AuditLogs.Tests;

internal sealed class EncryptedAuditEntity : AuditedEntity<Guid>
{
    #region Properties

    // Not deny-listed by name, marked [Encrypted] -> must be redacted regardless of name (LOG-002).
    [Encrypted]
    public string Iban { get; set; } = string.Empty;

    // [Encrypted] AND [AuditLog] on the same property -> [Encrypted] must still win (R2), exactly like
    // [SensitiveData] already does for DRK-582.
    [Encrypted]
    [AuditLog]
    public string NationalId { get; set; } = string.Empty;

    // Marked with a *different* type also named "EncryptedAttribute", from an unrelated namespace ->
    // proves the match is by attribute type name, not type identity (R1).
    [ForeignAttributes.EncryptedAttribute]
    public string ForeignMarkedField { get; set; } = string.Empty;

    // Not attributed, name matches no deny-list fragment -> plaintext control case.
    public string BranchName { get; set; } = string.Empty;

    #endregion

    #region Methods

    public void SetCreatedOn(string byUser, DateTimeOffset? on = null) => SetCreatedBy(byUser, on);
    public void SetUpdatedOn(string byUser, DateTimeOffset? on = null) => SetUpdatedBy(byUser, on);

    #endregion
}

// Separate entity carrying a shadow property (no backing CLR property) so BuildPlan/BuildAuditLog see a
// PropertyInfo of null for it, proving IsEncrypted does not throw on that input (R3).
internal sealed class ShadowPropertyAuditEntity : AuditedEntity<Guid>
{
    #region Methods

    public void SetCreatedOn(string byUser, DateTimeOffset? on = null) => SetCreatedBy(byUser, on);
    public void SetUpdatedOn(string byUser, DateTimeOffset? on = null) => SetUpdatedBy(byUser, on);

    #endregion
}

internal sealed class EncryptedAuditDbContext(DbContextOptions<EncryptedAuditDbContext> options)
    : DbContext(options)
{
    #region Properties

    public DbSet<EncryptedAuditEntity> Entities => Set<EncryptedAuditEntity>();

    public DbSet<ShadowPropertyAuditEntity> ShadowEntities => Set<ShadowPropertyAuditEntity>();

    #endregion

    #region Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Shadow property: exists only in the EF model, has no CLR PropertyInfo.
        modelBuilder.Entity<ShadowPropertyAuditEntity>().Property<string>("ShadowNote");
    }

    #endregion
}

/// <summary>
///     Tests for DRK-1569 (LOG-002 / SEC-005): a property carrying an attribute named
///     <c>EncryptedAttribute</c> must be redacted in every audit entry exactly like
///     <see cref="DKNet.EfCore.Abstractions.Attributes.SensitiveDataAttribute" />, and the audit hook's
///     publisher-failure log must never carry field values.
/// </summary>
public class EncryptedPropertyRedactionTests
{
    #region Helpers

    private static string NewDbPath() =>
        Path.Combine(Path.GetTempPath(), $"encrypted_audit_{Guid.NewGuid():N}.db");

    private static DbContextOptions<EncryptedAuditDbContext> BuildOptions(string dbPath) =>
        new DbContextOptionsBuilder<EncryptedAuditDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .EnableSensitiveDataLogging()
            .Options;

    private static async Task<EntityEntry> SeedAndModifyAsync(EncryptedAuditDbContext ctx)
    {
        await ctx.Database.EnsureCreatedAsync();

        var entity = new EncryptedAuditEntity
        {
            Iban = "IBAN-1",
            NationalId = "National-1",
            ForeignMarkedField = "Foreign-1",
            BranchName = "Branch-1"
        };
        entity.SetCreatedOn("creator");
        ctx.Add(entity);
        await ctx.SaveChangesAsync();

        entity.Iban = "IBAN-2";
        entity.NationalId = "National-2";
        entity.ForeignMarkedField = "Foreign-2";
        entity.BranchName = "Branch-2";
        entity.SetUpdatedOn("updater");
        ctx.ChangeTracker.DetectChanges();

        return ctx.Entry(entity);
    }

    private static ServiceProvider BuildHookProvider(Action<IServiceCollection> registerPublishers, string dbPath,
        ILoggerProvider? loggerProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            if (loggerProvider is not null) b.AddProvider(loggerProvider);
        });
        services.AddEfCoreAuditHook<EncryptedAuditDbContext>();
        registerPublishers(services);
        services.AddDbContextWithHook<EncryptedAuditDbContext>((_, o) => o.UseSqlite($"Data Source={dbPath}"));
        return services.BuildServiceProvider();
    }

    private static void RegisterKeyedPublisher(IServiceCollection services, IAuditLogPublisher publisher) =>
        services.AddKeyedScoped<IAuditLogPublisher>(typeof(EncryptedAuditDbContext).FullName, (_, _) => publisher);

    #endregion

    #region Tests

    [Fact]
    public async Task EncryptedPropertyRedaction_NonDenyListedName_IsRedacted()
    {
        await using var ctx = new EncryptedAuditDbContext(BuildOptions(NewDbPath()));
        var entry = await SeedAndModifyAsync(ctx);

        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities,
            AuditPropertyPolicy.RedactSensitive)!;

        var change = log.Changes.Single(c => c.FieldName == nameof(EncryptedAuditEntity.Iban));
        change.OldValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
        change.NewValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
    }

    [Fact]
    public async Task EncryptedPropertyRedaction_WinsOverAuditLogAttribute_IsRedacted()
    {
        await using var ctx = new EncryptedAuditDbContext(BuildOptions(NewDbPath()));
        var entry = await SeedAndModifyAsync(ctx);

        var redactSensitiveLog = entry.BuildAuditLog(EntityState.Modified,
            AuditLogBehaviour.IncludeAllAuditedEntities, AuditPropertyPolicy.RedactSensitive)!;
        var redactSensitiveChange =
            redactSensitiveLog.Changes.Single(c => c.FieldName == nameof(EncryptedAuditEntity.NationalId));
        redactSensitiveChange.OldValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
        redactSensitiveChange.NewValue.ShouldBe(SensitiveDataPatterns.RedactedValue);

        var onlyAttributedLog = entry.BuildAuditLog(EntityState.Modified,
            AuditLogBehaviour.IncludeAllAuditedEntities, AuditPropertyPolicy.OnlyAttributedProperties)!;
        var onlyAttributedChange =
            onlyAttributedLog.Changes.Single(c => c.FieldName == nameof(EncryptedAuditEntity.NationalId));
        onlyAttributedChange.OldValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
        onlyAttributedChange.NewValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
    }

    [Fact]
    public async Task EncryptedPropertyRedaction_ForeignAttributeOfSameName_IsRedacted()
    {
        await using var ctx = new EncryptedAuditDbContext(BuildOptions(NewDbPath()));
        var entry = await SeedAndModifyAsync(ctx);

        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities,
            AuditPropertyPolicy.RedactSensitive)!;

        var change = log.Changes.Single(c => c.FieldName == nameof(EncryptedAuditEntity.ForeignMarkedField));
        change.OldValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
        change.NewValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
    }

    [Fact]
    public async Task EncryptedPropertyRedaction_ShadowProperty_DoesNotThrow()
    {
        await using var ctx = new EncryptedAuditDbContext(BuildOptions(NewDbPath()));
        await ctx.Database.EnsureCreatedAsync();

        var entity = new ShadowPropertyAuditEntity();
        entity.SetCreatedOn("creator");
        ctx.Add(entity);
        ctx.Entry(entity).Property("ShadowNote").CurrentValue = "Note-1";
        await ctx.SaveChangesAsync();

        ctx.Entry(entity).Property("ShadowNote").CurrentValue = "Note-2";
        entity.SetUpdatedOn("updater");
        ctx.ChangeTracker.DetectChanges();

        var entry = ctx.Entry(entity);

        Should.NotThrow(() => entry.BuildAuditLog(EntityState.Modified,
            AuditLogBehaviour.IncludeAllAuditedEntities, AuditPropertyPolicy.RedactSensitive));
    }

    [Fact]
    public async Task PlainProperty_StillCapturedInPlaintext()
    {
        await using var ctx = new EncryptedAuditDbContext(BuildOptions(NewDbPath()));
        var entry = await SeedAndModifyAsync(ctx);

        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities,
            AuditPropertyPolicy.RedactSensitive)!;

        var change = log.Changes.Single(c => c.FieldName == nameof(EncryptedAuditEntity.BranchName));
        change.OldValue.ShouldBe("Branch-1");
        change.NewValue.ShouldBe("Branch-2");
    }

    [Fact]
    public async Task EncryptedEntity_ThroughHook_PublishedRedacted()
    {
        var publisher = new TestPublisher();
        await using var provider = BuildHookProvider(s => RegisterKeyedPublisher(s, publisher), NewDbPath());
        await using var scope = provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<EncryptedAuditDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var entity = new EncryptedAuditEntity { Iban = "IBAN-live", NationalId = "N-live", BranchName = "B-live" };
        entity.SetCreatedOn("creator");
        ctx.Add(entity);
        await ctx.SaveChangesAsync(); // create: no field changes captured yet
        publisher.Clear();

        entity.Iban = "IBAN-live-2";
        entity.SetUpdatedOn("updater");
        await ctx.SaveChangesAsync(); // update: this is the entry that must carry the redacted value

        var received = publisher.Received.Single();
        var change = received.Changes.Single(c => c.FieldName == nameof(EncryptedAuditEntity.Iban));
        change.NewValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
    }

    [Fact]
    public async Task PublisherFailure_LogError_CarriesNoFieldValues()
    {
        const string sentinel = "IBAN-SENTINEL-DO-NOT-LEAK";
        var logProvider = new CapturingLoggerProvider();
        await using var provider = BuildHookProvider(
            s => RegisterKeyedPublisher(s, new AsyncFailingPublisher()), NewDbPath(), logProvider);
        await using var scope = provider.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<EncryptedAuditDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        var entity = new EncryptedAuditEntity { Iban = "initial-iban", NationalId = "N-1", BranchName = "B-1" };
        entity.SetCreatedOn("creator");
        ctx.Add(entity);
        await ctx.SaveChangesAsync(); // create: publish fails silently, no field changes captured yet

        // Update: this save's audit entry carries an Iban field change, the vector this test pins.
        entity.Iban = sentinel;
        entity.SetUpdatedOn("updater");
        await ctx.SaveChangesAsync(); // must not throw despite the failing publisher

        logProvider.Messages.ShouldContain(m =>
            m.Level == LogLevel.Error && m.Message.Contains(nameof(EncryptedAuditEntity)));
        logProvider.Messages.ShouldNotContain(m => m.Message.Contains(sentinel));
    }

    #endregion
}
