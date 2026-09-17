using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.Responses;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>
///     An in-memory <see cref="ILoggerProvider" /> that records every log entry it sees, so DRK-1484's abandoned-
///     request scenario (R6) can assert no <see cref="LogLevel.Error" /> entry was written for a request the
///     caller disconnected from.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Category, LogLevel Level, string Message)>
        _entries = new();

    public IReadOnlyCollection<(string Category, LogLevel Level, string Message)> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            owner._entries.Enqueue((category, logLevel, formatter(state, exception)));
    }
}

/// <summary>
///     A real minimal-API host for DRK-1328/DRK-1484's ledger scenarios — <c>/ledger/...</c> routes dispatching
///     through a real in-memory <see cref="SlimMessageBus.IMessageBus" />, with FluentValidation's endpoint
///     auto-validation always turned on for the validated route (that switch is orthogonal to the error-response
///     setting: even the no-setting scenarios need it to reach today's validation-problem body). Whether
///     <see cref="ErrorResponseServiceCollectionExtensions.AddErrorResponses" /> is called at all is the one thing
///     each test controls, via <paramref name="configureErrorResponses" /> on <see cref="CreateAsync" />; the
///     hosting environment (<c>Development</c> vs <c>Production</c>) is the other, via
///     <paramref name="environmentName" />.
/// </summary>
public sealed class LedgerTestHost : IAsyncDisposable
{
    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    /// <summary>Captures every log entry written while the host is running (DRK-1484 §5's abandoned-request scenario).</summary>
    public CapturingLoggerProvider Logs { get; } = new();

    /// <summary>
    ///     Whether the <c>/ledger/abandon</c> handler observed <c>HttpContext.Response.HasStarted</c> as
    ///     <see langword="true" /> once the caller disconnected. Set only after that endpoint has run once.
    /// </summary>
    public bool? AbandonedRequestResponseHasStarted { get; private set; }

    public static async Task<LedgerTestHost> CreateAsync(
        Action<ErrorResponseOptions>? configureErrorResponses = null,
        string environmentName = "Development")
    {
        var host = new LedgerTestHost();
        await host.InitializeAsync(configureErrorResponses, environmentName);
        return host;
    }

    private async Task InitializeAsync(Action<ErrorResponseOptions>? configureErrorResponses, string environmentName)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = environmentName;
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(Logs);
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        builder.Services.AddValidatorsFromAssemblyContaining<ValidatedCloseAccountGroupCommandValidator>();
        if (configureErrorResponses is not null)
            builder.Services.AddErrorResponses(configureErrorResponses);

        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(LedgerTestHost).Assembly)
            .AddChildBus(
                "Memory",
                mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(LedgerTestHost).Assembly)));

        var app = builder.Build();
        var group = app.MapGroup("/ledger").AddFluentValidationAutoValidation();

        group.MapPost<CloseAccountGroupCommand>("/groups/close");
        group.MapPost<ValidatedCloseAccountGroupCommand>("/groups/close-validated");
        group.MapPost<FindAccountGroupCommand, AccountGroupResult>("/groups/find");
        group.MapPost<ExplodeAccountGroupCommand>("/groups/explode");
        group.MapPost<RenameOwnedAccountGroupCommand>("/groups/rename-owned");
        group.MapPost<CreateDuplicateAccountGroupCommand>("/groups/create-duplicate");

        // DRK-1484 @11 — dispatches through the short-form Response() with no [FromServices] ErrorResponseOptions
        // threaded at all, proving the helper reads a registered setting on its own (row 9), unlike the mapped
        // endpoints above which still thread the option explicitly (row 13 retires that parameter).
        group.MapPost(
            "/groups/close-no-options-threaded",
            async (SlimMessageBus.IMessageBus bus, CloseAccountGroupCommand request) =>
            {
                var rs = await bus.Send(request);
                return rs.Response();
            });

        group.MapGet(
            "/abandon",
            async (HttpContext ctx) =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, ctx.RequestAborted);
                }
                finally
                {
                    AbandonedRequestResponseHasStarted = ctx.Response.HasStarted;
                }

                return Results.Ok();
            });

        await app.StartAsync();
        _app = app;
        Client = app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (_app is null) return;
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
