// New tests to cover the [SensitiveData] attribute redaction path (DRK-582).

using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.AuditLogs;
using DKNet.EfCore.AuditLogs.Internals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Shouldly;

namespace EfCore.AuditLogs.Tests;

internal sealed class DeclaredSensitiveEntity : AuditedEntity<Guid>
{
    #region Properties

    // Not sensitive by name, but explicitly declared sensitive -> must be redacted unconditionally.
    [SensitiveData]
    public string DisplayLabel { get; set; } = string.Empty;

    // Declared sensitive AND attributed for audit capture -> SensitiveData still wins (per the
    // attribute's doc comment: it redacts "even when AuditLogAttribute is also applied").
    [AuditLog]
    [SensitiveData]
    public string InternalCode { get; set; } = string.Empty;

    // Not declared sensitive, name does not match a known pattern -> plaintext control case.
    public string PlainField { get; set; } = string.Empty;

    // No [SensitiveData], but the name contains "Token" -> still redacted by the existing name guess.
    public string CustomerToken { get; set; } = string.Empty;

    #endregion

    #region Methods

    public void SetCreatedOn(string byUser, DateTimeOffset? on = null) => SetCreatedBy(byUser, on);
    public void SetUpdatedOn(string byUser, DateTimeOffset? on = null) => SetUpdatedBy(byUser, on);

    #endregion
}

internal sealed class DeclaredSensitiveDbContext(DbContextOptions<DeclaredSensitiveDbContext> options)
    : DbContext(options)
{
    #region Properties

    public DbSet<DeclaredSensitiveEntity> Entities => Set<DeclaredSensitiveEntity>();

    #endregion
}

/// <summary>
///     Tests for DRK-582: <see cref="SensitiveDataAttribute" /> must redact a property unconditionally,
///     regardless of its name or whether <see cref="AuditLogAttribute" /> is also applied, while the
///     existing name-pattern guess keeps working for properties with no explicit declaration.
/// </summary>
public class SensitiveDataAttributeRedactionTests
{
    #region Methods

    private static DbContextOptions<DeclaredSensitiveDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<DeclaredSensitiveDbContext>()
            .UseSqlite(
                $"Data Source={Path.Combine(Path.GetTempPath(), $"sensitive_attr_{Guid.NewGuid():N}.db")}")
            .EnableSensitiveDataLogging()
            .Options;

    private static async Task<EntityEntry> SeedAndModifyAsync(DeclaredSensitiveDbContext ctx)
    {
        await ctx.Database.EnsureCreatedAsync();

        var entity = new DeclaredSensitiveEntity
        {
            DisplayLabel = "Label-1",
            InternalCode = "Code-1",
            PlainField = "Plain-1",
            CustomerToken = "Token-1"
        };
        entity.SetCreatedOn("creator");
        ctx.Add(entity);
        await ctx.SaveChangesAsync();

        entity.DisplayLabel = "Label-2";
        entity.InternalCode = "Code-2";
        entity.PlainField = "Plain-2";
        entity.CustomerToken = "Token-2";
        entity.SetUpdatedOn("updater");
        ctx.ChangeTracker.DetectChanges();

        return ctx.Entry(entity);
    }

    [Fact]
    public async Task BuildAuditLog_SensitiveDataAttribute_RedactsPropertyRegardlessOfName()
    {
        await using var ctx = new DeclaredSensitiveDbContext(BuildOptions());
        var entry = await SeedAndModifyAsync(ctx);

        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities,
            AuditPropertyPolicy.RedactSensitive)!;

        var change = log.Changes.Single(c => c.FieldName == nameof(DeclaredSensitiveEntity.DisplayLabel));
        change.OldValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
        change.NewValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
    }

    [Fact]
    public async Task BuildAuditLog_SensitiveDataAttribute_RedactsEvenWhenAlsoAuditLogAttributed()
    {
        await using var ctx = new DeclaredSensitiveDbContext(BuildOptions());
        var entry = await SeedAndModifyAsync(ctx);

        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities,
            AuditPropertyPolicy.RedactSensitive)!;

        var change = log.Changes.Single(c => c.FieldName == nameof(DeclaredSensitiveEntity.InternalCode));
        change.OldValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
        change.NewValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
    }

    [Fact]
    public async Task BuildAuditLog_UndeclaredCustomerTokenProperty_RedactedByNameGuess()
    {
        await using var ctx = new DeclaredSensitiveDbContext(BuildOptions());
        var entry = await SeedAndModifyAsync(ctx);

        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities,
            AuditPropertyPolicy.RedactSensitive)!;

        var change = log.Changes.Single(c => c.FieldName == nameof(DeclaredSensitiveEntity.CustomerToken));
        change.OldValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
        change.NewValue.ShouldBe(SensitiveDataPatterns.RedactedValue);
    }

    [Fact]
    public async Task BuildAuditLog_PlainUnattributedProperty_CapturedInPlaintext()
    {
        await using var ctx = new DeclaredSensitiveDbContext(BuildOptions());
        var entry = await SeedAndModifyAsync(ctx);

        var log = entry.BuildAuditLog(EntityState.Modified, AuditLogBehaviour.IncludeAllAuditedEntities,
            AuditPropertyPolicy.RedactSensitive)!;

        var change = log.Changes.Single(c => c.FieldName == nameof(DeclaredSensitiveEntity.PlainField));
        change.OldValue.ShouldBe("Plain-1");
        change.NewValue.ShouldBe("Plain-2");
    }

    #endregion
}
