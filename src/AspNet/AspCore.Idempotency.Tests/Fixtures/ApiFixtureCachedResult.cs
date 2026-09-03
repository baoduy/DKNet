// <copyright file="IdempotencyWebApplicationFactory.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AspCore.Idempotency.Tests.Fixtures;

/// <summary>
///     Web application factory for testing idempotency endpoints.
///     Provides a minimal web host configured with idempotency services and test endpoints.
/// </summary>
public sealed class ApiFixtureCachedResult : WebApplicationFactory<ApiTests.Program>, IAsyncLifetime
{
    #region Properties

    public HttpClient? HttpClient { get; private set; }

    #endregion

    #region Methods

    /// <summary>
    ///     Configures the web host builder for testing.
    /// </summary>
    /// <param name="builder">The web host builder.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set test environment
        builder.UseEnvironment(Environments.Development);
        // Program.cs already calls the parameterless AddIdempotentKey() first; WebApplicationFactory
        // runs this ConfigureServices after it, so the parameterless overload here would be a no-op
        // per its first-caller-wins rule. Naming the store explicitly takes the replacement path
        // instead, which applies this fixture's config after the default's own.
        builder.ConfigureServices(s =>
            s.AddIdempotentKey<IdempotencyInMemoryStore>(
                c => c.ConflictHandling = IdempotentConflictHandling.CachedResult));
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    public Task InitializeAsync()
    {
        HttpClient ??= CreateClient();
        return Task.CompletedTask;
    }

    #endregion
}