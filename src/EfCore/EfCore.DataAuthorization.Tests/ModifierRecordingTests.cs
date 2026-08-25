using Microsoft.EntityFrameworkCore;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     Covers DRK-721's automatic modifier recording: <see cref="DKNet.EfCore.DataAuthorization.Internals.DataOwnerHook" />
///     stamps <c>UpdatedBy</c>/<c>UpdatedOn</c> on every modified audited entity with the current context's
///     ownership key, unless a domain method already recorded an explicit modifier for this change set.
/// </summary>
public class ModifierRecordingTests(ModifierRecordingFixture fixture) : IClassFixture<ModifierRecordingFixture>
{
    #region Methods

    [Fact]
    public async Task Modification_OnNeverModifiedRecord_RecordsAcceptingContextAsModifier()
    {
        // Arrange: a record created by "Steven" (current context's ownership key) that has never been modified
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root("Acme Trading", "Steven");
        await db.AddAsync(entity);
        await db.SaveChangesAsync();
        entity.UpdatedBy.ShouldBeNull();

        // Act: an ordinary domain mutation that never touches the modifier itself
        var before = DateTimeOffset.UtcNow;
        entity.Rename("Acme Trading Pte Ltd");
        await db.SaveChangesAsync();
        var after = DateTimeOffset.UtcNow;

        // Assert
        entity.UpdatedBy.ShouldBe("Steven");
        entity.UpdatedOn.ShouldNotBeNull();
        entity.UpdatedOn!.Value.ShouldBeGreaterThanOrEqualTo(before);
        entity.UpdatedOn.Value.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public async Task LaterModification_SupersedesAnEarlierRecordedModifier()
    {
        // Arrange: an earlier modification explicitly recorded "acme-night-batch" at a real, fixed time
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root("Acme Contact", "Steven");
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        var earlierModifiedOn = new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero);
        entity.RecordModifier("acme-night-batch", earlierModifiedOn);
        await db.SaveChangesAsync();
        entity.UpdatedBy.ShouldBe("acme-night-batch");
        entity.UpdatedOn.ShouldBe(earlierModifiedOn);

        // Act: a later, ordinary modification under the current context ("Steven")
        entity.Rename("Acme Contact - billing@acme.com.sg");
        await db.SaveChangesAsync();

        // Assert: the latest save wins — no longer the earlier modifier or its time
        entity.UpdatedBy.ShouldBe("Steven");
        entity.UpdatedBy.ShouldNotBe("acme-night-batch");
        entity.UpdatedOn.ShouldNotBe(earlierModifiedOn);
    }

    [Fact]
    public async Task ExplicitlyRecordedModifier_AndItsTime_AreBothPreserved()
    {
        // Arrange
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root("Acme Merchant", "Steven");
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Act: a domain operation records its own modifier and time for this change set
        var explicitModifiedOn = new DateTimeOffset(2026, 3, 12, 2, 15, 0, TimeSpan.Zero);
        entity.RecordModifier("night-batch-operator", explicitModifiedOn);
        await db.SaveChangesAsync();

        // Assert: the hook leaves both untouched instead of overwriting with the context's ownership key
        entity.UpdatedBy.ShouldBe("night-batch-operator");
        entity.UpdatedOn.ShouldBe(explicitModifiedOn);
    }

    [Fact]
    public async Task MarkingARecordDeleted_IsRecordedAsAnOrdinaryModification()
    {
        // Arrange: a record that has never been modified
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root("Acme Merchant To Delete", "Steven");
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Act: the only property that changes is the delete marker
        entity.MarkDeleted();
        await db.SaveChangesAsync();

        // Assert: automatic recording doesn't special-case which property changed
        entity.IsDeleted.ShouldBeTrue();
        entity.UpdatedBy.ShouldBe("Steven");
    }

    [Fact]
    public async Task CreationRecording_IsUnaffectedByModifierRecording()
    {
        // Arrange & Act: a brand-new record, added and saved, never modified since
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root("Acme Pte Ltd", "Steven");
        var createdBy = entity.CreatedBy;
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Assert: creator is unchanged, no modifier is recorded, and the fallback still reports the creator
        entity.CreatedBy.ShouldBe(createdBy);
        entity.UpdatedBy.ShouldBeNull();
        entity.LastModifiedBy.ShouldBe(entity.CreatedBy);
    }

    [Fact]
    public async Task RejectedTenantReassignment_StillRecordsAcceptingContextAsModifier()
    {
        // Arrange: a record owned by "Steven", the current context may not act for "intruder"
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root("Acme Owned Merchant", "Steven");
        await db.AddAsync(entity);
        await db.SaveChangesAsync();

        // Act: attempt to move ownership to a key outside GetAccessibleKeys()
        entity.SetOwnedBy("intruder");
        await Should.NotThrowAsync(() => db.SaveChangesAsync());

        // Assert: ownership never moved, yet the attempted change is still recorded as a modification —
        // someone did touch the record, even though what they touched it with was rejected.
        var reloaded = await db.Set<Root>().AsNoTracking().FirstAsync(r => r.Id == entity.Id);
        reloaded.OwnedBy.ShouldBe("Steven");
        entity.UpdatedBy.ShouldBe("Steven");
    }

    #endregion
}

/// <summary>
///     Covers the "no ownership key" scenario: background jobs, seeding, and migrations run with no
///     ownership key, so automatic modifier recording must stay silent and the save must still succeed.
/// </summary>
public class ModifierRecordingWithoutOwnershipKeyTests(EmptyOwnerKeyFixture fixture)
    : IClassFixture<EmptyOwnerKeyFixture>
{
    #region Methods

    [Fact]
    public async Task Modification_WithNoOwnershipKey_RecordsNothingAndStillSaves()
    {
        // Arrange: EmptyOwnerKeyProvider returns "" for GetOwnershipKey(); AccessibleKeys still allows "Steven"
        var db = fixture.Provider.GetRequiredService<DddContext>();
        var entity = new Root("Acme Holdings", "Steven");
        await db.AddAsync(entity);
        await db.SaveChangesAsync();
        var createdBy = entity.CreatedBy;

        // Act
        entity.Rename("Acme Holdings Pte Ltd");
        await Should.NotThrowAsync(() => db.SaveChangesAsync());

        // Assert: still no modifier recorded, and the fallback still reports the creator
        entity.UpdatedBy.ShouldBeNull();
        entity.LastModifiedBy.ShouldBe(createdBy);
    }

    #endregion
}

/// <summary>
///     Covers the "not opted into data authorization" scenario: <see cref="PlainDbContext" /> has neither
///     <c>AddDataOwnerProvider</c> nor <c>AddDbContextWithHook</c> wired, so <see cref="DKNet.EfCore.DataAuthorization.Internals.DataOwnerHook" />
///     never runs against it.
/// </summary>
public class ModifierRecordingOptedOutConsumerTests(OptedOutConsumerFixture fixture)
    : IClassFixture<OptedOutConsumerFixture>
{
    #region Methods

    [Fact]
    public async Task Modification_OnConsumerNotOptedIntoDataAuthorization_RecordsNothingAndStillSaves()
    {
        // Arrange: a plain audited entity in a context with no data-authorization wiring at all
        var entity = new PlainAuditedEntity("Acme Pte Ltd");
        await fixture.Db.AddAsync(entity);
        await fixture.Db.SaveChangesAsync();

        // Act
        entity.Rename("Acme Ventures Pte Ltd");
        await Should.NotThrowAsync(() => fixture.Db.SaveChangesAsync());

        // Assert: automatic recording never fires for a consumer that hasn't opted in
        entity.UpdatedBy.ShouldBeNull();
        entity.UpdatedOn.ShouldBeNull();
    }

    #endregion
}

/// <summary>
///     Pure-entity unit coverage (no DbContext): recording a modification must never alter the creator,
///     mirroring the invariant creation recording already guarantees.
/// </summary>
public class ModifierRecordingEntityInvariantTests
{
    #region Methods

    [Fact]
    public void RecordModifier_ModificationRecorded_CreatorIsNeverAltered()
    {
        // Arrange
        var entity = new Root("Acme Merchant", "Steven");
        var originalCreatedBy = entity.CreatedBy;

        // Act
        entity.RecordModifier("globex-holdings");

        // Assert
        entity.CreatedBy.ShouldBe(originalCreatedBy);
    }

    #endregion
}
