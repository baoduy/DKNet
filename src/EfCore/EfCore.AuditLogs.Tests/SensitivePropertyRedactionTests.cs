// New tests to cover AuditPropertyPolicy redaction / strict-mode paths (SEC-005).

using System.Collections.Concurrent;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.AuditLogs.Internals;
using DKNet.EfCore.Hooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EfCore.AuditLogs.Tests;

internal sealed class SensitiveAuditEntity : AuditedEntity<Guid>
{
    #region Properties

    // Sensitive by name, no attribute -> redacted under RedactSensitive, omitted under OnlyAttributedProperties.
    public string Password { get; set; } = string.Empty;

    // Sensitive by name but explicitly attributed -> always captured in plaintext.
    [AuditLog] public DateTimeOffset TokenExpiryUtc { get; set; }

    // Non-sensitive, no attribute -> plaintext under RedactSensitive, omitted under OnlyAttributedProperties.
    public string DisplayName { get; set; } = string.Empty;

    #endregion

    #region Methods

    public void SetCreatedOn(string byUser, DateTimeOffset? on = null) => SetCreatedBy(byUser, on);
    public void SetUpdatedOn(string byUser, DateTimeOffset? on = null) => SetUpdatedBy(byUser, on);

    #endregion
}

internal sealed class SensitiveAuditDbContext(DbContextOptions<SensitiveAuditDbContext> options) : DbContext(options)
{
    #region Properties

    public DbSet<SensitiveAuditEntity> SensitiveEntities => Set<SensitiveAuditEntity>();

    #endregion
}

internal sealed class SensitiveCapturingPublisher : IAuditLogPublisher
{
    #region Fields

    private static readonly ConcurrentBag<AuditLogEntry> _logs = [];

    #endregion

    #region Properties

    public static IReadOnlyCollection<AuditLogEntry> Logs => _logs;

    #endregion

    #region Methods

    public static void Clear()
    {
        while (_logs.TryTake(out _))
        {
        }
    }

    public Task PublishAsync(IEnumerable<AuditLogEntry> logs, CancellationToken cancellationToken = default)
    {
        foreach (var l in logs) _logs.Add(l);

        return Task.CompletedTask;
    }

    #endregion
}

public class SensitivePropertyRedactionTests
{
    #region Methods

    private static ServiceProvider BuildProvider(AuditPropertyPolicy propertyPolicy, string dbPath)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEfCoreAuditLogs<SensitiveAuditDbContext, SensitiveCapturingPublisher>(
            propertyPolicy: propertyPolicy);
        services.AddDbContextWithHook<SensitiveAuditDbContext>((_, o) =>
        {
            o.UseSqlite($"Data Source={dbPath}");
            o.EnableSensitiveDataLogging();
        });
        return services.BuildServiceProvider();
    }

    private static string NewDbPath() => Path.Combine(Path.GetTempPath(), $"sensitive_{Guid.NewGuid():N}.db");

    private static async Task<(ServiceProvider provider, Guid id)> SeedAndUpdateAsync(AuditPropertyPolicy policy)
    {
        var db = NewDbPath();
        var provider = BuildProvider(policy, db);
        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SensitiveAuditDbContext>();
            await ctx.Database.EnsureCreatedAsync();
        }

        Guid id;
        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SensitiveAuditDbContext>();
            var e = new SensitiveAuditEntity
            {
                Password = "hunter2",
                TokenExpiryUtc = DateTimeOffset.UtcNow,
                DisplayName = "Alice"
            };
            e.SetCreatedOn("creator");
            ctx.Add(e);
            await ctx.SaveChangesAsync();
            id = e.Id;
        }

        SensitiveCapturingPublisher.Clear();

        await using (var scope = provider.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SensitiveAuditDbContext>();
            var e = await ctx.SensitiveEntities.FirstAsync(x => x.Id == id);
            e.Password = "hunter3";
            e.TokenExpiryUtc = e.TokenExpiryUtc.AddHours(1);
            e.DisplayName = "Alice B.";
            e.SetUpdatedOn("updater");
            await ctx.SaveChangesAsync();
        }

        await Task.Delay(1000);
        return (provider, id);
    }

    [Fact]
    public async Task RedactSensitive_UnannotatedSensitiveProperty_IsRedacted()
    {
        var (provider, _) = await SeedAndUpdateAsync(AuditPropertyPolicy.RedactSensitive);
        await using var _ = provider;

        var log = SensitiveCapturingPublisher.Logs.Single(l =>
            l.EntityName == nameof(SensitiveAuditEntity) && l.Action == AuditLogAction.Updated);
        var change = log.Changes.Single(c => c.FieldName == nameof(SensitiveAuditEntity.Password));
        change.OldValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
        change.NewValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
    }

    [Fact]
    public async Task RedactSensitive_AttributedSensitiveProperty_CapturedInPlaintext()
    {
        var (provider, _) = await SeedAndUpdateAsync(AuditPropertyPolicy.RedactSensitive);
        await using var _ = provider;

        var log = SensitiveCapturingPublisher.Logs.Single(l =>
            l.EntityName == nameof(SensitiveAuditEntity) && l.Action == AuditLogAction.Updated);
        var change = log.Changes.Single(c => c.FieldName == nameof(SensitiveAuditEntity.TokenExpiryUtc));
        change.OldValue.ShouldNotBe(SensitiveDataPatterns.RedactedValue);
        change.NewValue.ShouldNotBe(SensitiveDataPatterns.RedactedValue);
    }

    [Fact]
    public async Task RedactSensitive_NonSensitiveProperty_CapturedInPlaintext()
    {
        var (provider, _) = await SeedAndUpdateAsync(AuditPropertyPolicy.RedactSensitive);
        await using var _ = provider;

        var log = SensitiveCapturingPublisher.Logs.Single(l =>
            l.EntityName == nameof(SensitiveAuditEntity) && l.Action == AuditLogAction.Updated);
        var change = log.Changes.Single(c => c.FieldName == nameof(SensitiveAuditEntity.DisplayName));
        change.OldValue.ShouldBe("Alice");
        change.NewValue.ShouldBe("Alice B.");
    }

    [Fact]
    public async Task OnlyAttributedProperties_CapturesOnlyAttributedProperty()
    {
        var (provider, _) = await SeedAndUpdateAsync(AuditPropertyPolicy.OnlyAttributedProperties);
        await using var _ = provider;

        var log = SensitiveCapturingPublisher.Logs.Single(l =>
            l.EntityName == nameof(SensitiveAuditEntity) && l.Action == AuditLogAction.Updated);
        log.Changes.ShouldContain(c => c.FieldName == nameof(SensitiveAuditEntity.TokenExpiryUtc));
        log.Changes.ShouldNotContain(c => c.FieldName == nameof(SensitiveAuditEntity.Password));
        log.Changes.ShouldNotContain(c => c.FieldName == nameof(SensitiveAuditEntity.DisplayName));
    }

    #endregion
}
