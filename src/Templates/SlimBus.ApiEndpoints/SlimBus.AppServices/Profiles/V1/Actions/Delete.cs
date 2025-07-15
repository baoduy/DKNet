namespace SlimBus.AppServices.Profiles.V1.Actions;

public record DeleteProfileCommand : BaseCommand, Fluents.Requests.INoResponse
{
    public required Guid Id { get; init; }
}

internal sealed class DeleteProfileCommandHandler(ICustomerProfileRepo repository)
    : Fluents.Requests.IHandler<DeleteProfileCommand>
{
    public async Task<IResultBase> OnHandle(DeleteProfileCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
            return Result.Fail("The Id is in valid.")
                .WithError(new Error("The Id is in valid.") { Metadata = { ["field"] = nameof(request.Id) } });

        var profile = await repository.FindAsync(request.Id, cancellationToken);

        if (profile == null)
            return Result.Fail($"The Profile {request.Id} is not found.");

        repository.Delete(profile);

        return Result.Ok();
    }
}