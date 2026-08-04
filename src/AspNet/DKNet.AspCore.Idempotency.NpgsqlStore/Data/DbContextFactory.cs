// <copyright file="DbContextFactory.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.AspCore.Idempotency.NpgsqlStore.Data;

[ExcludeFromCodeCoverage]
internal sealed class DbContextFactory : IDesignTimeDbContextFactory<IdempotencyDbContext>
{
    public IdempotencyDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("IDEMPOTENCY_NPGSQL_CONNECTION")
                   ?? throw new InvalidOperationException(
                       "Set the IDEMPOTENCY_NPGSQL_CONNECTION environment variable to run EF Core design-time tools.");

        var service = new ServiceCollection()
            .AddLogging()
            .AddIdempotencyNpgsqlStore(conn)
            .BuildServiceProvider();

        return service.GetRequiredService<IdempotencyDbContext>();
    }
}
