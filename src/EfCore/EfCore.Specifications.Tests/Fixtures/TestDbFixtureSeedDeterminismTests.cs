// <copyright file="TestDbFixtureSeedDeterminismTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace EfCore.Specifications.Tests.Fixtures;

/// <summary>
///     BDD-style tests verifying <see cref="TestDbFixture" /> seeds deterministic, fully-covering data
///     (DRK-1460 acceptance criteria).
/// </summary>
public class TestDbFixtureSeedDeterminismTests(TestDbFixture fixture) : IClassFixture<TestDbFixture>
{
    #region Fields

    private readonly TestDbContext _context = fixture.Db!;

    #endregion

    #region AC 1 — Every OrderStatus member is represented

    [Fact]
    public async Task SeededOrdersCoverEveryOrderStatus()
    {
        var seededStatuses = await _context.Orders.Select(o => o.Status).Distinct().ToListAsync();

        foreach (var status in Enum.GetValues<OrderStatus>())
            seededStatuses.ShouldContain(status);
    }

    #endregion

    #region AC 2 — Seeding is reproducible across fixture instances

    [Fact]
    public async Task SeedIsReproducibleAcrossFixtureInstances()
    {
        var first = new TestDbFixture();
        var second = new TestDbFixture();

        try
        {
            await first.InitializeAsync();
            await second.InitializeAsync();

            var firstStatuses = await first.Db!.Orders.OrderBy(o => o.Id).Select(o => o.Status).ToListAsync();
            var secondStatuses = await second.Db!.Orders.OrderBy(o => o.Id).Select(o => o.Status).ToListAsync();
            firstStatuses.ShouldBe(secondStatuses);

            var firstProducts = await first.Db!.Products.OrderBy(p => p.Id)
                .Select(p => new { p.Name, p.Price, p.StockQuantity, p.IsActive })
                .ToListAsync();
            var secondProducts = await second.Db!.Products.OrderBy(p => p.Id)
                .Select(p => new { p.Name, p.Price, p.StockQuantity, p.IsActive })
                .ToListAsync();
            firstProducts.ShouldBe(secondProducts);
        }
        finally
        {
            await first.DisposeAsync();
            await second.DisposeAsync();
        }
    }

    #endregion

    #region AC 3 — First seeded order stays Pending

    [Fact]
    public async Task FirstSeededOrderIsPending()
    {
        var firstOrder = await _context.Orders.OrderBy(o => o.Id).FirstAsync();

        firstOrder.Status.ShouldBe(OrderStatus.Pending);
    }

    #endregion
}
