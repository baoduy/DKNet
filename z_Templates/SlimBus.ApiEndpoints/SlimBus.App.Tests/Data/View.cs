namespace SlimBus.App.Tests.Data;

public record View
{
    public Guid Id { get; set; } = Guid.Empty;
    public string Name { get; set; } = null!;
}