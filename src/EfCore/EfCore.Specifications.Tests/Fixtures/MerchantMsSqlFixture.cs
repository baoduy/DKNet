// <copyright file="MerchantMsSqlFixture.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DotNet.Testcontainers.Builders;
using EfCore.Specifications.Tests.TestEntities;
using Testcontainers.MsSql;

namespace EfCore.Specifications.Tests.Fixtures;

/// <summary>
///     Spins up a real SQL Server (via TestContainers) seeded with merchants for the three-key
///     keyset pagination integration scenario (country ascending, revenue descending, identifier ascending).
/// </summary>
/// <remarks>
///     mssql/server ships x64-only images. Do not run this fixture on an ARM device (no working local
///     image) - verify it on the GitHub-hosted x64 runner instead:
///     <c>gh workflow run remote-tests.yml --ref &lt;branch&gt; -f project=EfCore/EfCore.Specifications.Tests</c>.
///     See root <c>CLAUDE.md</c> "Remote test verification".
/// </remarks>
public sealed class MerchantMsSqlFixture : IAsyncLifetime
{
    #region Fields

    private const string MssqlImage = "mcr.microsoft.com/mssql/server:2022-latest";

    private MsSqlContainer? _container;

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

        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    public async Task InitializeAsync()
    {
        _container = new MsSqlBuilder(MssqlImage)
            .WithPassword($"A{Guid.NewGuid():N}a1!")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilMessageIsLogged("SQL Server is now ready for client connections"))
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<MerchantDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;

        Db = new MerchantDbContext(options);
        await Db.Database.EnsureCreatedAsync();

        // Declared order (Country asc, Revenue desc, Id asc) matches insertion order below, so the
        // three-way tie on (Country, Revenue) at Id 1-3 exercises the Id tiebreak a two-key ordering
        // cannot express.
        var merchants = new[]
        {
            new Merchant { Id = 1, Country = "Indonesia", Revenue = 500m, Name = "Jakarta Foods" },
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
