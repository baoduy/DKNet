using DKNet.SlimBus.Extensions;
using FluentResults;
using FluentValidation;

namespace AspCore.Extensions.Tests.Fixtures;

// Fixtures for DRK-1328 (one error-response setting for refused commands and refused input). Kept separate
// from Requests.cs / EndpointConfigSupport.cs so this cycle's fixtures read as one unit: a command whose
// handler refuses a "business refusal" business rule, the same rule expressed as a validator, an idempotency
// conflict (unit-level only, no endpoint), and a lookup that fails with NotFoundError.

/// <summary>
///     Machine-readable discriminators the ledger service's failures carry — a command attaches these via
///     FluentResults <see cref="Error.Metadata" />["Code"], a validator via <see cref="FluentValidation.Results.ValidationFailure.ErrorCode" />.
/// </summary>
public static class LedgerErrorCodes
{
    public const string BusinessRefusal = "business-refusal";
    public const string IdempotencyConflict = "idempotency-conflict";
}

public sealed record AccountGroupResult
{
    public string Name { get; init; } = string.Empty;
}

/// <summary>Always refuses — "the account group still holds accounts" — carrying <see cref="LedgerErrorCodes.BusinessRefusal" /> as FluentResults metadata.</summary>
public sealed record CloseAccountGroupCommand : Fluents.Requests.INoResponse
{
    public string GroupName { get; init; } = string.Empty;
}

internal sealed class CloseAccountGroupHandler : Fluents.Requests.IHandler<CloseAccountGroupCommand>
{
    public Task<IResultBase> OnHandle(CloseAccountGroupCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResultBase>(Result.Fail(
            new Error($"Account group '{request.GroupName}' still holds accounts.")
                .WithMetadata("Code", LedgerErrorCodes.BusinessRefusal)));
}

/// <summary>
///     Same business rule as <see cref="CloseAccountGroupCommand" />, but refused by
///     <see cref="ValidatedCloseAccountGroupCommandValidator" /> instead of the handler — proves the same
///     discriminator reaches an error-response setting from either failure path.
/// </summary>
public sealed record ValidatedCloseAccountGroupCommand : Fluents.Requests.INoResponse
{
    public string GroupName { get; init; } = string.Empty;

    /// <summary>Test-controlled stand-in for "the group still holds accounts" — a real validator would look this up.</summary>
    public bool StillHoldsAccounts { get; init; }
}

public sealed class ValidatedCloseAccountGroupCommandValidator : AbstractValidator<ValidatedCloseAccountGroupCommand>
{
    public ValidatedCloseAccountGroupCommandValidator() =>
        RuleFor(x => x.StillHoldsAccounts)
            .Equal(false)
            .WithErrorCode(LedgerErrorCodes.BusinessRefusal)
            .WithMessage(x => $"Account group '{x.GroupName}' still holds accounts.");
}

internal sealed class ValidatedCloseAccountGroupCommandHandler
    : Fluents.Requests.IHandler<ValidatedCloseAccountGroupCommand>
{
    public Task<IResultBase> OnHandle(
        ValidatedCloseAccountGroupCommand request,
        CancellationToken cancellationToken) =>
        Task.FromResult<IResultBase>(Result.Ok());
}

/// <summary>Fails with <see cref="NotFoundError" /> when <see cref="Exists" /> is false — proves 404 needs no setting.</summary>
public sealed record FindAccountGroupCommand : Fluents.Requests.IWitResponse<AccountGroupResult>
{
    public string GroupName { get; init; } = string.Empty;
    public bool Exists { get; init; }
}

internal sealed class FindAccountGroupHandler : Fluents.Requests.IHandler<FindAccountGroupCommand, AccountGroupResult>
{
    public Task<IResult<AccountGroupResult>> OnHandle(
        FindAccountGroupCommand request,
        CancellationToken cancellationToken) =>
        Task.FromResult<IResult<AccountGroupResult>>(request.Exists
            ? Result.Ok(new AccountGroupResult { Name = request.GroupName })
            : Result.Fail<AccountGroupResult>(new NotFoundError($"Account group '{request.GroupName}' was not found.")));
}
