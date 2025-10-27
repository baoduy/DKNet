namespace SlimBus.App.Tests.Data;

public class TestDataModel(Guid id, string name)
{
    #region Constructors

    public TestDataModel(string name) : this(Guid.NewGuid(), name)
    {
    }

    #endregion

    #region Properties

    public Guid Id { get; } = id;
    public string Name { get; } = name;

    #endregion
}