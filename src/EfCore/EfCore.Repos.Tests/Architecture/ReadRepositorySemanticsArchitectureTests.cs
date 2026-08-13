// <copyright file="ReadRepositorySemanticsArchitectureTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using NetArchTest.Rules;
using Shouldly;

namespace EfCore.Repos.Tests.Architecture;

/// <summary>
///     Enforces that <see cref="IReadRepository{TEntity}" /> has exactly one read implementation in
///     <c>DKNet.EfCore.Repos</c>. Every implementer must derive from <see cref="ReadRepository{TEntity}" /> so that
///     the read semantics — most importantly whether <c>Query()</c> tracks entities — are defined in one place.
///     A second, independently written copy of the read methods drifts: consumers coding against the same
///     <see cref="IReadRepository{TEntity}" /> contract then get different tracking behaviour depending on which
///     concrete type DI happened to hand them.
///     <para>
///         This is a Tier-2 baseline rule (architecture-review DRK-320): there is exactly one known offender today,
///         <see cref="Repository{TEntity}" />, which re-implements the read surface on top of
///         <see cref="WriteRepository{TEntity}" /> and whose <c>Query()</c> returns a TRACKED queryable while
///         <see cref="ReadRepository{TEntity}" />'s returns <c>AsNoTracking()</c>. It is on the allow-list below.
///         The allow-list must only ever SHRINK — when the duplication is collapsed, delete its entry. Do not add
///         new names to it.
///     </para>
/// </summary>
public sealed class ReadRepositorySemanticsArchitectureTests
{
    #region Fields

    private const string OffenderTypeName = "Repository`1";

    /// <summary>
    ///     Today's known offenders. Must only shrink — never add a name here to silence a new violation.
    /// </summary>
    private static readonly string[] KnownViolations = [OffenderTypeName];

    #endregion

    #region Methods

    [Fact]
    public void ReadRepositoryImplementations_ExceptKnownOffenders_MustDeriveFromReadRepository()
    {
        var result = Types.InAssembly(typeof(ReadRepository<>).Assembly)
            .That()
            .ImplementInterface(typeof(IReadRepository<>))
            .And()
            .DoNotHaveName(KnownViolations)
            .And()
            .DoNotHaveName(nameof(ReadRepository<object>) + "`1")
            .Should()
            .Inherit(typeof(ReadRepository<>))
            .GetResult();

        var offenders = result.FailingTypeNames ?? [];
        result.IsSuccessful.ShouldBeTrue(
            "IReadRepository<T> must have a single read implementation so tracking semantics cannot diverge — a " +
            "second hand-written copy of Query()/FindAsync()/CountAsync() is how one implementation ends up " +
            "AsNoTracking() and the other tracked, silently turning reads into accidental writes at the consumer's " +
            "next SaveChanges(). New offenders: " + string.Join(", ", offenders) +
            ". Fix by deriving from ReadRepository<TEntity> instead of re-implementing the read surface; do not add " +
            "the type to the KnownViolations allow-list.");
    }

    [Fact]
    public void Rule_CanDetectTheKnownOffender()
    {
        // Self-check: the allow-listed offender genuinely does NOT derive from ReadRepository<T>. If this ever
        // passes, the rule above has gone blind (NetArchTest can no longer resolve the open generic base type)
        // and would silently stop enforcing anything.
        var result = Types.InAssembly(typeof(ReadRepository<>).Assembly)
            .That()
            .HaveName(OffenderTypeName)
            .Should()
            .Inherit(typeof(ReadRepository<>))
            .GetResult();

        result.IsSuccessful.ShouldBeFalse(
            $"The known offender {OffenderTypeName} must still be detected as NOT deriving from ReadRepository<T>; " +
            "if this assertion fails the enforcement rule can no longer see the duplicated read surface and is " +
            "worthless.");
    }

    #endregion
}
