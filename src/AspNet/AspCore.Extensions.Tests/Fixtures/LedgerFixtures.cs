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

    /// <summary>DRK-1484 §5 "One setting changes the status of a refused request" — mapped to 409.</summary>
    public const string Precondition = "precondition";
}

/// <summary>Always refuses with <see cref="LedgerErrorCodes.Precondition" /> — stands in for "a group with this name already exists".</summary>
public sealed record CreateDuplicateAccountGroupCommand : Fluents.Requests.INoResponse
{
    public string GroupName { get; init; } = string.Empty;
}

internal sealed class CreateDuplicateAccountGroupHandler
    : Fluents.Requests.IHandler<CreateDuplicateAccountGroupCommand>
{
    public Task<IResultBase> OnHandle(CreateDuplicateAccountGroupCommand request, CancellationToken cancellationToken) =>
        Task.FromResult<IResultBase>(Result.Fail(
            new Error($"Account group '{request.GroupName}' already exists.")
                .WithMetadata("Code", LedgerErrorCodes.Precondition)));
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

/// <summary>
///     Raised by <see cref="RenameOwnedAccountGroupHandler" /> to stand in for "this group belongs to another
///     team" — an unexpected error whose kind (not its route) must drive the configured status (DRK-1484 §5
///     "One setting changes the status of an unexpected error").
/// </summary>
public sealed class OwnershipRefusalException(string message) : Exception(message);

/// <summary>DRK-1484 — always throws, to exercise an unhandled error the setting maps to 403.</summary>
public sealed record RenameOwnedAccountGroupCommand : Fluents.Requests.INoResponse
{
    public string GroupName { get; init; } = string.Empty;
}

internal sealed class RenameOwnedAccountGroupHandler : Fluents.Requests.IHandler<RenameOwnedAccountGroupCommand>
{
    public Task<IResultBase> OnHandle(RenameOwnedAccountGroupCommand request, CancellationToken cancellationToken) =>
        throw new OwnershipRefusalException($"Account group '{request.GroupName}' belongs to another team.");
}

/// <summary>
///     DRK-1484 — always throws a plain unexpected error. <see cref="SensitiveDetail" /> is the message text an
///     outside-development body must never repeat (R4).
/// </summary>
public sealed record ExplodeAccountGroupCommand : Fluents.Requests.INoResponse
{
    public const string SensitiveDetail = "connection string: Server=internal-db;Password=hunter2";
}

internal sealed class ExplodeAccountGroupHandler : Fluents.Requests.IHandler<ExplodeAccountGroupCommand>
{
    public Task<IResultBase> OnHandle(ExplodeAccountGroupCommand request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(ExplodeAccountGroupCommand.SensitiveDetail);
}
