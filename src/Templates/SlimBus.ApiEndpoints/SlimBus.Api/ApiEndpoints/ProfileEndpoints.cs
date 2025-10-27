using SlimBus.Api.Configs.Idempotency;
using SlimBus.AppServices.Profiles.V1.Actions;
using SlimBus.AppServices.Profiles.V1.Queries;

namespace SlimBus.Api.ApiEndpoints;

internal sealed class ProfileV1Endpoint : IEndpointConfig
{
    #region Properties

    public string GroupEndpoint => "/profiles";

    public int Version => 1;

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetPage<PageProfilePageQuery, ProfileResult>("")
            .WithDescription("Get all profiles");
        group.MapGet<ProfileQuery, ProfileResult?>("{id:guid}")
            .WithDescription("Get profile by id");
        group.MapPost<CreateProfileCommand, ProfileResult>("")
            .AddIdempotencyFilter()
            .WithDescription("Create profile. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
                             "X-Idempotency-Key: {IdempotencyKey} <br/>");
        group.MapPut<UpdateProfileCommand, ProfileResult>("{id:guid}")
            .WithDescription("Update profile by id");
        group.MapDelete<DeleteProfileCommand>("{id:guid}")
            .WithDescription("Delete profile by id");
    }

    #endregion
}

internal sealed class ProfileV2Endpoint : IEndpointConfig
{
    #region Properties

    public string GroupEndpoint => "/profiles";

    public int Version => 2;

    #endregion

    #region Methods

    public void Map(RouteGroupBuilder group)
    {
        group.MapGetPage<PageProfilePageQuery, ProfileResult>("")
            .WithDescription("Get all profiles");
        group.MapGet<ProfileQuery, ProfileResult?>("{id:guid}")
            .WithDescription("Get profile by id");
        group.MapPost<CreateProfileCommand, ProfileResult>("")
            .AddIdempotencyFilter()
            .WithDescription("Create profile. <br/><br/> Note: Idempotency key is required in the header. <br/>" +
                             "X-Idempotency-Key: {IdempotencyKey} <br/>");
        group.MapPut<UpdateProfileCommand, ProfileResult>("{id:guid}")
            .WithDescription("Update profile by id");
        group.MapDelete<DeleteProfileCommand>("{id:guid}")
            .WithDescription("Delete profile by id");
    }

    #endregion
}