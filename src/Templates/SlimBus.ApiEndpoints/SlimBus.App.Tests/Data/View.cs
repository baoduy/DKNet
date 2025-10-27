namespace SlimBus.App.Tests.Data;

public record View
{
    #region Properties

    public Guid Id { get; set; } = Guid.Empty;
    public string Name { get; set; } = null!;

    #endregion
}