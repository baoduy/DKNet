// <copyright file="ApiFixtureCustomScope.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.AspCore.Idempotency;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace AspCore.Idempotency.Tests.Fixtures;

/// <summary>
///     Web application factory that overrides the default scope resolver chain with a fixed custom resolver.
/// </summary>
public sealed class ApiFixtureCustomScope : WebApplicationFactory<ApiTests.Program>, IAsyncLifetime
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
        builder.ConfigureServices(s =>
            s.AddIdempotentKey(c =>
            {
                c.ConflictHandling = IdempotentConflictHandling.ConflictResponse;
                c.KeyScopeResolver = _ => "tenant:acme";
            }));
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
