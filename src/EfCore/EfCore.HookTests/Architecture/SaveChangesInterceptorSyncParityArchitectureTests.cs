// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SaveChangesInterceptorSyncParityArchitectureTests.cs
// Description: Tier-2 architecture rule — a SaveChangesInterceptor that overrides an async member must also
//              override its sync counterpart, or DbContext.SaveChanges() silently bypasses it.

using System.Reflection;
using DKNet.EfCore.Extensions.Snapshots;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EfCore.HookTests.Architecture;

/// <summary>
///     EF Core dispatches a synchronous <c>DbContext.SaveChanges()</c> to the synchronous interceptor members
///     only (<c>SavingChanges</c>, <c>SavedChanges</c>, <c>SaveChangesFailed</c>, <c>SaveChangesCanceled</c>) —
///     never to their <c>*Async</c> twins. An interceptor that overrides only the async member therefore does
///     nothing at all on a sync save: no exception, no log line, a normal return value. For the hook runner that
///     means the data-owner hook, audit stamping and domain-event publishing are all silently skipped.
///     <para>
///         Tier-2 rule (architecture-review DRK-1896, finding DRK-1899): <see cref="KnownViolations" /> is the
///         baseline of today's offenders. A new offender fails the build; fixing an offender must delete its
///         entry — the list only ever shrinks, and <see cref="KnownViolations_AreAllStillViolations" /> fails if
///         an entry goes stale.
///     </para>
/// </summary>
public sealed class SaveChangesInterceptorSyncParityArchitectureTests
{
    #region Fields

    /// <summary>
    ///     Today's offenders as <c>{FullTypeName}.{AsyncMember}</c>. Only ever remove entries (fix tracked in
    ///     DRK-1899); never add one to make a new interceptor pass.
    /// </summary>
    private static readonly HashSet<string> KnownViolations =
    [
        "DKNet.EfCore.Hooks.Internals.HookRunnerInterceptor.SavingChangesAsync",
        "DKNet.EfCore.Hooks.Internals.HookRunnerInterceptor.SavedChangesAsync",
        "DKNet.EfCore.Hooks.Internals.HookRunnerInterceptor.SaveChangesFailedAsync"
    ];

    /// <summary>Async member → the sync member EF Core calls for <c>SaveChanges()</c> instead.</summary>
    private static readonly (string Async, string Sync)[] MemberPairs =
    [
        (nameof(SaveChangesInterceptor.SavingChangesAsync), nameof(SaveChangesInterceptor.SavingChanges)),
        (nameof(SaveChangesInterceptor.SavedChangesAsync), nameof(SaveChangesInterceptor.SavedChanges)),
        (nameof(SaveChangesInterceptor.SaveChangesFailedAsync), nameof(SaveChangesInterceptor.SaveChangesFailed)),
        (nameof(SaveChangesInterceptor.SaveChangesCanceledAsync), nameof(SaveChangesInterceptor.SaveChangesCanceled))
    ];

    /// <summary>The DKNet save-pipeline assemblies this test project references.</summary>
    private static readonly Assembly[] ScannedAssemblies =
    [
        typeof(HookRunnerInterceptor).Assembly,
        typeof(SnapshotContext).Assembly
    ];

    #endregion

    #region Methods

    private static List<string> FindViolations() =>
    [
        .. from type in ScannedAssemblies.SelectMany(a => a.GetTypes())
        where !type.IsAbstract && typeof(SaveChangesInterceptor).IsAssignableFrom(type)
        from pair in MemberPairs
        where DeclaresOverride(type, pair.Async) && !DeclaresOverride(type, pair.Sync)
        select $"{type.FullName}.{pair.Async}"
    ];

    private static bool DeclaresOverride(Type type, string memberName) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Any(m => m.Name == memberName && m.GetBaseDefinition().DeclaringType != type);

    [Fact]
    public void SaveChangesInterceptors_OverridingAnAsyncMember_MustAlsoOverrideTheSyncCounterpart()
    {
        var newViolations = FindViolations().Where(v => !KnownViolations.Contains(v)).ToList();

        newViolations.ShouldBeEmpty(
            "EF Core routes DbContext.SaveChanges() to the sync interceptor members only, so an interceptor that " +
            "overrides just the *Async member is silently skipped on every sync save — hooks, owner stamping, " +
            "audit and domain events all vanish with no error. Override the sync counterpart too (run the same " +
            "logic, or throw to fail closed). Do not add the member to KnownViolations. Offenders: " +
            string.Join(", ", newViolations));
    }

    [Fact]
    public void KnownViolations_AreAllStillViolations()
    {
        var stale = KnownViolations.Except(FindViolations()).ToList();

        stale.ShouldBeEmpty(
            "These KnownViolations entries no longer violate the rule — the baseline only shrinks, so delete them " +
            "to lock the fix in (DRK-1899). Stale entries: " + string.Join(", ", stale));
    }

    [Fact]
    public void Rule_ScansAtLeastOneSaveChangesInterceptor()
    {
        // Self-check: if the scanned assemblies ever stop containing an interceptor (moved, renamed, split into a
        // new package), the rule above would pass vacuously and enforce nothing.
        ScannedAssemblies.SelectMany(a => a.GetTypes())
            .Count(t => !t.IsAbstract && typeof(SaveChangesInterceptor).IsAssignableFrom(t))
            .ShouldBeGreaterThan(0,
                "The parity rule must scan at least one concrete SaveChangesInterceptor; point ScannedAssemblies " +
                "at the assembly that now holds the save-pipeline interceptors.");
    }

    #endregion
}
