using SlimBus.Domains.Features.Profiles.Entities;

namespace SlimBus.AppServices.Profiles.V1.Actions;

[MapsTo(typeof(CustomerProfile))]
public record UpdateProfileCommand : BaseCommand, Fluents.Requests.IWitResponse<ProfileResult>
{
    public required Guid Id { get; init; }

    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Name { get; init; }
}

internal sealed class UpdateProfileCommandHandler(
    IMapper mapper,
    ICustomerProfileRepo repo) : Fluents.Requests.IHandler<UpdateProfileCommand, ProfileResult>
{
    public async Task<IResult<ProfileResult>> OnHandle(UpdateProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Id == Guid.Empty)
            return Result.Fail<ProfileResult>("The Id is in valid.");

        var profile = await repo.FindAsync(request.Id, cancellationToken);

        if (profile == null)
            return Result.Fail<ProfileResult>($"The Profile {request.Id} is not found.");

        //Update Here
        profile.Update(null, request.Name, request.Phone, null, request.ByUser!);

        //Add Event

        //Return result
        return Result.Ok(mapper.Map<ProfileResult>(profile));
    }
}