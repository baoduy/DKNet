// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: ErrorResponseEndpointTests.cs
// Description: DRK-1328 §5 @integration scenarios — one error-response setting shapes both a refused command
// and refused input, dispatched over real HTTP through LedgerTestHost. RED SHA + per-scenario table: see the
// Acceptance-tests sub-task completion report; every scenario here fails today with NotImplementedException
// from the not-yet-implemented AddErrorResponses / ToProblemDetails(options) (DRK-1337).

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AspCore.Extensions.Tests.Fixtures;

namespace AspCore.Extensions.Tests.Endpoints;

public class ErrorResponseEndpointTests
{
    #region Methods

    private static async Task AssertIsProblemDocumentAsync(HttpResponseMessage response)
    {
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.ShouldNotBeNull();
        body.ShouldContainKey("status");
    }

    // --- Scenario: A refused command answers with the configured status -------------------------------------

    [Fact]
    public async Task CloseAccountGroup_BusinessRefusalMappedTo422_CommandHandlerRefusal_Returns422ProblemDocument()
    {
        await using var host = await LedgerTestHost.CreateAsync(o =>
            o.StatusCode = ctx => ctx.Errors.Any(e => e.Code == LedgerErrorCodes.BusinessRefusal) ? 422 : null);

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close", new CloseAccountGroupCommand { GroupName = "treasury-ops" });

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        await AssertIsProblemDocumentAsync(response);
    }

    // --- Scenario: Refused input answers with the same configured status ------------------------------------

    [Fact]
    public async Task CloseAccountGroup_BusinessRefusalMappedTo422_ValidatorRefusal_Returns422ProblemDocument()
    {
        await using var host = await LedgerTestHost.CreateAsync(o =>
            o.StatusCode = ctx => ctx.Errors.Any(e => e.Code == LedgerErrorCodes.BusinessRefusal) ? 422 : null);

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close-validated",
            new ValidatedCloseAccountGroupCommand { GroupName = "treasury-ops", StillHoldsAccounts = true });

        response.StatusCode.ShouldBe((HttpStatusCode)422);
        await AssertIsProblemDocumentAsync(response);
    }

    // --- Scenario Outline: A body member the service adds appears on every kind of failure -------------------

    [Fact]
    public async Task AddedBodyMember_CommandHandlerFailure_ErrorCodeMemberPresent()
    {
        await using var host = await LedgerTestHost.CreateAsync(o =>
            o.Customize = (pd, _) => pd.Extensions["error-code"] = "ledger-refusal");

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close", new CloseAccountGroupCommand { GroupName = "treasury-ops" });

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.ShouldNotBeNull();
        body.ShouldContainKey("error-code");
    }

    [Fact]
    public async Task AddedBodyMember_ValidatorFailure_ErrorCodeMemberPresent()
    {
        await using var host = await LedgerTestHost.CreateAsync(o =>
            o.Customize = (pd, _) => pd.Extensions["error-code"] = "ledger-refusal");

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close-validated",
            new ValidatedCloseAccountGroupCommand { GroupName = "treasury-ops", StillHoldsAccounts = true });

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.ShouldNotBeNull();
        body.ShouldContainKey("error-code");
    }

    // --- Scenario: Not-found still answers 404 with no setting registered -----------------------------------

    [Fact]
    public async Task FindAccountGroup_NoErrorResponseSettingRegistered_NotFound_Returns404()
    {
        await using var host = await LedgerTestHost.CreateAsync();

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/find", new FindAccountGroupCommand { GroupName = "treasury-ops", Exists = false });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    // --- Scenario Outline: A service that registers no setting still gets DRK-1484's unified command-failure shape ----

    [Fact]
    public async Task NoErrorResponseSettingRegistered_CommandHandlerRefusal_Returns400UnifiedErrorItemList()
    {
        await using var host = await LedgerTestHost.CreateAsync();

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close", new CloseAccountGroupCommand { GroupName = "treasury-ops" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // DRK-1484: the short-form Response() always answers with the unified body — errors is an ErrorItem[]
        // (objects, not flat strings), even with no ErrorResponseOptions registered at all.
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        errors.ShouldHaveSingleItem();
        errors[0].GetProperty("message").GetString().ShouldBe("Account group 'treasury-ops' still holds accounts.");
    }

    [Fact]
    public async Task NoErrorResponseSettingRegistered_ValidatorRefusal_Returns400FieldToMessagesMap()
    {
        await using var host = await LedgerTestHost.CreateAsync();

        var response = await host.Client.PostAsJsonAsync(
            "/ledger/groups/close-validated",
            new ValidatedCloseAccountGroupCommand { GroupName = "treasury-ops", StillHoldsAccounts = true });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.ShouldNotBeNull();
        // Today's shape: SharpGrip's default factory puts a field->messages map under "errors".
        body.ShouldContainKey("errors");
        var errorsJson = body["errors"].ToString();
        errorsJson.ShouldNotBeNull();
        errorsJson.ShouldContain(nameof(ValidatedCloseAccountGroupCommand.StillHoldsAccounts));
    }

    #endregion
}
