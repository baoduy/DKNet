// <copyright file="IdempotencyKeyCapScriptTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using AspCore.Idempotency.NpgsqlStore.Tests.Fixtures;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace AspCore.Idempotency.NpgsqlStore.Tests.Integration;

/// <summary>
///     DRK-2221: the idempotent migration script an operator runs by hand (<c>dotnet ef migrations script
///     --idempotent</c>) applies the 500-character cap and can be re-run on the same database.
/// </summary>
[Collection("Api Collection")]
public sealed class IdempotencyKeyCapScriptTests(ApiFixture fixture)
{
    #region Methods

    [Fact]
    public async Task IdempotentScript_RunTwiceOnEmptyDatabase_NarrowsColumnTo500()
    {
        // Arrange - an empty database of its own and the full idempotent script
        var services = new ServiceCollection();
        services.AddIdempotencyNpgsqlStore(
            fixture.ConnectionString.Replace(fixture.DatabaseName, $"Idem_Script_{Guid.NewGuid():N}", StringComparison.Ordinal));
        await using var provider = services.BuildServiceProvider();
        await using var dbContext = provider.GetRequiredService<IdempotencyDbContext>();
        await dbContext.GetService<IRelationalDatabaseCreator>().CreateAsync();
        var script = dbContext.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        // Act - raw ADO.NET, so EF never rewrites the script text
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        for (var run = 0; run < 2; run++)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = script;
            await command.ExecuteNonQueryAsync();
        }

        // Assert
        (await dbContext.Database.SqlQueryRaw<string>(
                "SELECT data_type || ':' || COALESCE(character_maximum_length::text, '') || ':' || is_nullable AS \"Value\" " +
                "FROM information_schema.columns WHERE table_name = 'IdempotencyKeys' AND column_name = 'IdempotentKey'")
            .SingleAsync()).ShouldBe("character varying:500:NO");
    }

    #endregion
}
