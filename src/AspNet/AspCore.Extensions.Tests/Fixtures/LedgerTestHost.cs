using DKNet.AspCore.Extensions.Endpoints;
using DKNet.AspCore.Extensions.Responses;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests.Fixtures;

/// <summary>
///     A real minimal-API host for DRK-1328's ledger scenarios — <c>/ledger/...</c> routes dispatching through a
///     real in-memory <see cref="SlimMessageBus.IMessageBus" />, with FluentValidation's endpoint auto-validation
///     always turned on for the validated route (that switch is orthogonal to the error-response setting: even
///     the no-setting scenarios need it to reach today's validation-problem body). Whether
///     <see cref="ErrorResponseServiceCollectionExtensions.AddErrorResponses" /> is called at all is the one thing
///     each test controls, via <paramref name="configureErrorResponses" /> on <see cref="CreateAsync" />.
/// </summary>
public sealed class LedgerTestHost : IAsyncDisposable
{
    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    public static async Task<LedgerTestHost> CreateAsync(Action<ErrorResponseOptions>? configureErrorResponses = null)
    {
        var host = new LedgerTestHost();
        await host.InitializeAsync(configureErrorResponses);
        return host;
    }

    private async Task InitializeAsync(Action<ErrorResponseOptions>? configureErrorResponses)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

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
