namespace SlimBus.AppServices.Profiles.V1.Queries;

public record ProfileResult
{
    #region Properties

    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public required string Name { get; init; }

    public string? Phone { get; init; }

    #endregion
}