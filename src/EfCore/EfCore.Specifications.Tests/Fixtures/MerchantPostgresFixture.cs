// <copyright file="MerchantPostgresFixture.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using EfCore.Specifications.Tests.TestEntities;
using Testcontainers.PostgreSql;

namespace EfCore.Specifications.Tests.Fixtures;

/// <summary>
///     Spins up a real PostgreSQL (via TestContainers) seeded with merchants for the three-key
///     keyset pagination integration scenario (country ascending, revenue descending, identifier ascending).
/// </summary>
public sealed class MerchantPostgresFixture : IAsyncLifetime
{
    #region Fields

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    #endregion

    #region Properties

    /// <summary>
    ///     The seeded merchant the three-key ordering scenario pages forward/backward around.
    /// </summary>
    public Merchant BorneoTrading { get; private set; } = null!;

    public MerchantDbContext Db { get; private set; } = null!;

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (Db is not null) await Db.DisposeAsync();

        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<MerchantDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        Db = new MerchantDbContext(options);
        await Db.Database.EnsureCreatedAsync();

        // Declared order (Country asc, Revenue desc, Id asc) matches insertion order below, so the
        // three-way tie on (Country, Revenue) at Id 1-3 exercises the Id tiebreak a two-key ordering
        // cannot express.
        var merchants = new[]
        {
            // Exactly one merchant carries a TradingName; the other nine leave it NULL, so the null semantics
            // of the string and negation operations are observable as a 1-versus-9 split.
            new Merchant
            {
                Id = 1, Country = "Indonesia", Revenue = 500m, Name = "Jakarta Foods",
                TradingName = "Acme Trading"
            },
            new Merchant { Id = 2, Country = "Indonesia", Revenue = 500m, Name = "Borneo Trading" },
            new Merchant { Id = 3, Country = "Indonesia", Revenue = 500m, Name = "Sumatra Spice" },
            new Merchant { Id = 4, Country = "Indonesia", Revenue = 300m, Name = "Bali Crafts" },
            new Merchant { Id = 5, Country = "Malaysia", Revenue = 800m, Name = "Kuala Traders" },
            new Merchant { Id = 6, Country = "Malaysia", Revenue = 600m, Name = "Penang Exports" },
            new Merchant { Id = 7, Country = "Malaysia", Revenue = 600m, Name = "Melaka Textiles" },
            new Merchant { Id = 8, Country = "Malaysia", Revenue = 400m, Name = "Sabah Timber" },
            new Merchant { Id = 9, Country = "Singapore", Revenue = 900m, Name = "Marina Holdings" },
            new Merchant { Id = 10, Country = "Singapore", Revenue = 700m, Name = "Sentosa Imports" }
        };

        await Db.Merchants.AddRangeAsync(merchants);
        await Db.SaveChangesAsync();

        BorneoTrading = merchants[1];
    }

    #endregion
}
