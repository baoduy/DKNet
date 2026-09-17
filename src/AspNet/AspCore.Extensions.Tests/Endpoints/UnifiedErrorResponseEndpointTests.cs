// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: UnifiedErrorResponseEndpointTests.cs
// Description: DRK-1484 §5 @integration scenarios — a failed command, refused input and an unhandled exception
// all answer with the same body shape, driven by one ErrorResponseOptions setting, over real HTTP through
// LedgerTestHost. RED SHA + per-scenario table: see the Acceptance-tests sub-task completion report. Every
// scenario here fails today for a nameable reason — the unified body (Title/Type/traceId/ErrorItem[], no
// Detail) does not exist yet (DRK-1484 rows 4-5), unhandled exceptions are never caught (rows 11-12), and the
// short-form Response() helper still ignores any registered ErrorResponseOptions (row 9).

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;
using DKNet.AspCore.Extensions.Responses;
using Microsoft.Extensions.Logging;

namespace AspCore.Extensions.Tests.Endpoints;

public class UnifiedErrorResponseEndpointTests
{
    #region Methods

    /// <summary>
    ///     The standard shape every failure kind must carry (DRK-1484 §3): a title, a status, a type, a trace
    ///     identifier and an error list whose entries are objects (message/code?/field?) — never a flat string or
    ///     a field-name-keyed map.
    /// </summary>
    private static async Task<JsonElement> AssertStandardShapeAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("title", out var title).ShouldBeTrue("body must carry a title");
        title.GetString().ShouldBe("Error");

        body.TryGetProperty("status", out _).ShouldBeTrue("body must carry a status");
        body.TryGetProperty("type", out _).ShouldBeTrue("body must carry a type");
        body.TryGetProperty("traceId", out _).ShouldBeTrue("body must carry a trace identifier");

        body.TryGetProperty("errors", out var errors).ShouldBeTrue("body must carry an error list");
        errors.ValueKind.ShouldBe(JsonValueKind.Array);
        errors.GetArrayLength().ShouldBeGreaterThan(0);
        errors[0].ValueKind.ShouldBe(JsonValueKind.Object, "each error entry must be an object, not a plain string");
        errors[0].TryGetProperty("message", out _).ShouldBeTrue("each error entry must carry a message");

        return body;
    }

    // --- @1a/@1b/@1c: every failure kind answers with the same body shape ------------------------------------

    [Fact]
    public async Task StandardErrorSetting_HandlerRefuses_AnswersStandardShape()
    {
        await using var host = await LedgerTestHost.CreateAsync(_ => { });

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close", new CloseAccountGroupCommand { GroupName = "treasury-ops" });

        await AssertStandardShapeAsync(response);
    }

    [Fact]
    public async Task StandardErrorSetting_InputRulesBroken_AnswersStandardShape()
    {
        await using var host = await LedgerTestHost.CreateAsync(_ => { });

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close-validated",
            new ValidatedCloseAccountGroupCommand { GroupName = "treasury-ops", StillHoldsAccounts = true });

        await AssertStandardShapeAsync(response);
    }

    [Fact]
    public async Task StandardErrorSetting_UnexpectedError_AnswersStandardShape()
    {
        await using var host = await LedgerTestHost.CreateAsync(_ => { });

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/explode", new ExplodeAccountGroupCommand());

        await AssertStandardShapeAsync(response);
    }

    // --- @2: a refused field is named in the standard error list ---------------------------------------------

    [Fact]
    public async Task StandardErrorSetting_InputRulesBroken_ErrorListNamesTheRefusedField()
    {
        await using var host = await LedgerTestHost.CreateAsync(_ => { });

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close-validated",
            new ValidatedCloseAccountGroupCommand { GroupName = "treasury-ops", StillHoldsAccounts = true });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.ShouldHaveSingleItem();
        errors[0].GetProperty("field").GetString().ShouldBe(nameof(ValidatedCloseAccountGroupCommand.StillHoldsAccounts));
        errors[0].GetProperty("message").GetString()
            .ShouldBe("Account group 'treasury-ops' still holds accounts.");
    }

    // --- @3: one setting changes the status of a refused request (code "precondition" -> 409) ----------------

    [Fact]
    public async Task PreconditionMappedTo409_CommandHandlerRefusal_ReturnsConfiguredStatusWithCodeInErrorList()
    {
        await using var host = await LedgerTestHost.CreateAsync(
            o => o.StatusCode = ctx => ctx.Errors.Any(e => e.Code == LedgerErrorCodes.Precondition) ? 409 : null);

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/create-duplicate", new CreateDuplicateAccountGroupCommand { GroupName = "Widget" });

        response.StatusCode.ShouldBe((HttpStatusCode)409);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        var hasPreconditionCode = errors.Any(e =>
            e.TryGetProperty("code", out var c) && c.GetString() == LedgerErrorCodes.Precondition);
        hasPreconditionCode.ShouldBeTrue();
    }

    // --- @4: one setting changes the status of an unexpected error (ownership refusal -> 403) ----------------

    [Fact]
    public async Task OwnershipRefusalMappedTo403_UnexpectedError_ReturnsConfiguredStatus()
    {
        await using var host = await LedgerTestHost.CreateAsync(
            o => o.StatusCode = ctx => ctx.Exception is OwnershipRefusalException ? 403 : null);

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/rename-owned", new RenameOwnedAccountGroupCommand { GroupName = "treasury-ops" });

        response.StatusCode.ShouldBe((HttpStatusCode)403);
    }

    // --- @5a/@5b/@5c: one setting adds the same extra field to every failure kind -----------------------------

    [Fact]
    public async Task SupportReferenceAdded_HandlerRefuses_AppearsInBody()
    {
        await using var host = await LedgerTestHost.CreateAsync(
            o => o.Customize = (pd, _) => pd.Extensions["support-reference"] = "CAT-SUP");

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close", new CloseAccountGroupCommand { GroupName = "treasury-ops" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("support-reference").GetString().ShouldBe("CAT-SUP");
    }

    [Fact]
    public async Task SupportReferenceAdded_InputRulesBroken_AppearsInBody()
    {
        await using var host = await LedgerTestHost.CreateAsync(
            o => o.Customize = (pd, _) => pd.Extensions["support-reference"] = "CAT-SUP");

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close-validated",
            new ValidatedCloseAccountGroupCommand { GroupName = "treasury-ops", StillHoldsAccounts = true });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("support-reference").GetString().ShouldBe("CAT-SUP");
    }

    [Fact]
    public async Task SupportReferenceAdded_UnexpectedError_AppearsInBody()
    {
        await using var host = await LedgerTestHost.CreateAsync(
            o => o.Customize = (pd, _) => pd.Extensions["support-reference"] = "CAT-SUP");

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/explode", new ExplodeAccountGroupCommand());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("support-reference").GetString().ShouldBe("CAT-SUP");
    }

    // --- @6: an unexpected error tells the caller nothing it carried, outside development ---------------------

    [Fact]
    public async Task Production_UnexpectedError_TellsCallerNothingItCarried()
    {
        await using var host = await LedgerTestHost.CreateAsync(_ => { }, environmentName: "Production");

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/explode", new ExplodeAccountGroupCommand());

        response.StatusCode.ShouldBe((HttpStatusCode)500);

        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldNotContain(ExplodeAccountGroupCommand.SensitiveDetail);
        raw.ShouldNotContain(nameof(InvalidOperationException));

        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.ShouldHaveSingleItem();
        errors[0].GetProperty("message").GetString()
            .ShouldBe("An unexpected error occurred. Quote the trace-id when reporting this.");
    }

    // --- @7: an application supplies its own unhandled-error responder ---------------------------------------

    [Fact]
    public async Task CustomUnhandledErrorResponder_UnexpectedError_AnswersWithSuppliedBody()
    {
        await using var host = await LedgerTestHost.CreateAsync(o =>
            o.UnhandledError = _ => new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = 503,
                Title = "Error",
                Extensions = { ["retryDelaySeconds"] = 30 }
            });

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/explode", new ExplodeAccountGroupCommand());

        response.StatusCode.ShouldBe((HttpStatusCode)503);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("retryDelaySeconds").GetInt32().ShouldBe(30);
        body.TryGetProperty("traceId", out _).ShouldBeTrue("R8: every failure body carries a trace identifier, even a supplied one");
    }

    // --- @8: a setting that changes nothing keeps today's statuses ---------------------------------------------

    [Fact]
    public async Task StandardErrorSetting_ChangesNothing_NotFoundStillReturns404()
    {
        await using var host = await LedgerTestHost.CreateAsync(_ => { });

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/find", new FindAccountGroupCommand { GroupName = "treasury-ops", Exists = false });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // --- @9: an abandoned request is not answered and is not logged as an error --------------------------------

    [Fact]
    public async Task ClientDisconnect_AbandonedRequest_NoResponseStartedAndNoErrorLogged()
    {
        await using var host = await LedgerTestHost.CreateAsync(_ => { });

        using var cts = new CancellationTokenSource();
        var sendTask = host.Client.GetAsync("/ledger/abandon", cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => sendTask);

        // Give the server-side handler a moment to observe the abort and run its finally block.
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        host.AbandonedRequestResponseHasStarted.ShouldBe(false);
        host.Logs.Entries.ShouldNotContain(e => e.Level >= LogLevel.Error);
    }

    // --- @11: an endpoint naming no setting still gets it applied (row 9) --------------------------------------

    [Fact]
    public async Task RegisteredSetting_EndpointNamesNoOptionsParameter_StillApplied()
    {
        await using var host = await LedgerTestHost.CreateAsync(
            o => o.StatusCode = ctx => ctx.Errors.Any(e => e.Code == LedgerErrorCodes.BusinessRefusal) ? 422 : null);

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close-no-options-threaded",
            new CloseAccountGroupCommand { GroupName = "treasury-ops" });

        response.StatusCode.ShouldBe((HttpStatusCode)422);
    }

    // --- @12: a host calling only AddErrorResponses gets unhandled errors handled -------------------------------

    [Fact]
    public async Task HostCallsOnlyAddErrorResponses_UnexpectedError_IsHandledWithNoSecondRegistrationCall()
    {
        // LedgerTestHost never calls UseExceptionHandler/AddProblemDetails itself (R9) — AddErrorResponses is
        // the host's only step. Proves the registration call alone wires unhandled-error handling end to end.
        await using var host = await LedgerTestHost.CreateAsync(_ => { });

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/explode", new ExplodeAccountGroupCommand());

        await AssertStandardShapeAsync(response);
    }

    #endregion
}
