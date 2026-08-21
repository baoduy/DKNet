// <copyright file="KeysetThreeKeyOrderingIntegrationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Specifications.Extensions;
using MR.EntityFrameworkCore.KeysetPagination;

namespace EfCore.Specifications.Tests.Extensions;

/// <summary>
///     Integration coverage (real PostgreSQL via TestContainers) for paging over a three-key ordering
///     - country ascending, revenue descending, identifier ascending - per DRK-584's acceptance criteria.
/// </summary>
public class KeysetThreeKeyOrderingIntegrationTests : IClassFixture<MerchantPostgresFixture>
{
    #region Fields

    private readonly MerchantPostgresFixture _fixture;

    #endregion

    #region Constructors

    public KeysetThreeKeyOrderingIntegrationTests(MerchantPostgresFixture fixture) => _fixture = fixture;

    #endregion

    #region Methods

    /// <summary>
    ///     Given merchants ordered by country, then revenue descending, then identifier, paging after
    ///     "Borneo Trading" and then before it must follow, respectively precede, that merchant in the
    ///     declared order, and each page must report whether a further page exists ahead of and behind it.
    /// </summary>
    [Fact]
    public async Task ToKeysetPageAsync_PagingAroundAMerchant_FollowsAndPrecedesInThreeKeyOrder()
    {
        // Arrange
        Action<KeysetPaginationBuilder<Merchant>> configureKeyset = b => b
            .Ascending(m => m.Country)
            .Descending(m => m.Revenue)
            .Ascending(m => m.Id);

        // Act
        var forward = await _fixture.Db.Merchants.ToKeysetPageAsync(
            configureKeyset,
            pageSize: 3,
            direction: KeysetPaginationDirection.Forward,
            reference: _fixture.BorneoTrading);

        var backward = await _fixture.Db.Merchants.ToKeysetPageAsync(
            configureKeyset,
            pageSize: 3,
            direction: KeysetPaginationDirection.Backward,
            reference: _fixture.BorneoTrading);

        // Assert - forward page follows Borneo Trading (Indonesia/500/2) in country, revenue desc, id order:
        // the two remaining Indonesia rows (tie broken by Id), then the next country's top-revenue row.
        forward.Items.Select(m => m.Id).ShouldBe([3, 4, 5]);
        forward.HasPrevious.ShouldBeTrue("Jakarta Foods (Id 1) still precedes this page");
        forward.HasNext.ShouldBeTrue("Malaysia/Singapore rows beyond Id 5 remain");

        // Assert - backward page precedes Borneo Trading: only Jakarta Foods (Id 1) sits before it.
        backward.Items.Select(m => m.Id).ShouldBe([1]);
        backward.HasPrevious.ShouldBeFalse("Jakarta Foods is the first row in the declared order");
        backward.HasNext.ShouldBeTrue("Borneo Trading and everything after it still follows this page");
    }

    #endregion
}
