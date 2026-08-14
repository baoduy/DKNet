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
/// </summary>
public sealed class ReadRepositorySemanticsArchitectureTests
{
    #region Methods

    [Fact]
    public void ReadRepositoryImplementations_MustDeriveFromReadRepository()
    {
        var result = Types.InAssembly(typeof(ReadRepository<>).Assembly)
            .That()
            .ImplementInterface(typeof(IReadRepository<>))
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
            ". Fix by deriving from ReadRepository<TEntity> instead of re-implementing the read surface.");
    }

    #endregion
}
