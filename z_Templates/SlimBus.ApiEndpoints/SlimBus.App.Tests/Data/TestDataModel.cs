namespace SlimBus.App.Tests.Data;

public class TestDataModel(Guid id, string name)
{
    public TestDataModel(string name) : this(Guid.NewGuid(), name)
    {
    }

    public Guid Id { get; } = id;
    public string Name { get; } = name;
}