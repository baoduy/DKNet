using System.Reflection;
using HookContext = EfCore.HookTests.Data.HookContext;

namespace EfCore.HookTests.Hooks;

/// <summary>
///     Covers <see cref="HookFactory" />'s per-<see cref="Type" /> cache of provider key names (the DbContext
///     type hierarchy used to resolve keyed hooks) — it must still return the correct, type-specific hierarchy
///     for every distinct DbContext type, not a stale or shared result left over from another type.
/// </summary>
public class HookFactoryCachingTests
{
    #region Methods

    [Fact]
    public void GetProviderKeyNames_ForDifferentDbContextTypes_ReturnsEachTypesOwnHierarchy()
    {
        var method = typeof(HookFactory).GetMethod("GetProviderKeyNames",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        using var baseContext = new HookContext(
            new DbContextOptionsBuilder<HookContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        using var subContext = new SubHookContext(
            new DbContextOptionsBuilder<HookContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        // Act: resolve the subclass first, then the base type, then the subclass again — exercising the
        // cache in both directions so neither entry can leak into the other.
        var subKeys = (string[])method.Invoke(null, [subContext])!;
        var baseKeys = (string[])method.Invoke(null, [baseContext])!;
        var subKeysAgain = (string[])method.Invoke(null, [subContext])!;

        // Assert: the base type's hierarchy never includes the subclass...
        baseKeys.ShouldBe([typeof(HookContext).FullName, typeof(DbContext).FullName], ignoreOrder: true);

        // ...and the subclass's hierarchy includes itself, its base, and DbContext, on every call.
        subKeys.ShouldBe(
            [typeof(SubHookContext).FullName, typeof(HookContext).FullName, typeof(DbContext).FullName],
            ignoreOrder: true);
        subKeysAgain.ShouldBe(subKeys, ignoreOrder: true);
    }

    #endregion
}

/// <summary>
///     A subclass of <see cref="HookContext" /> used only to prove <see cref="HookFactory" />'s cached
///     provider-key-names lookup is keyed by the exact runtime type, not shared across a type hierarchy.
/// </summary>
internal sealed class SubHookContext(DbContextOptions<HookContext> options) : HookContext(options);
