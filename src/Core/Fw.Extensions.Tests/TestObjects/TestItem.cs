using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Fw.Extensions.Tests.TestObjects;

public enum TestEnumObject
{
    [Display(Name = "Enum 1")] Enum1,
    Enum2
}

public interface ITem
{
    #region Properties

    int Id { get; set; }

    string Name { get; set; }

    #endregion
}

public class TestItem : ITem
{
    #region Properties

    public int Id { get; set; }

    public string Details { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    #endregion
}

public class TestItem2 : ITem
{
    #region Properties

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    #endregion
}

public class TestItem3 : ITem, IDisposable
{
    #region Constructors

    public TestItem3()
    {
    }

    public TestItem3(string name) => this.Name = name;

    #endregion

    #region Properties

    public bool BoolValue { get; set; }

    public bool IsDisposed { get; private set; }

    public decimal DecimalValue { get; set; }

    public int Id { get; set; }

    public int IntValue { get; set; }

    public int? NullableIntValue { get; set; }

    [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    private object PrivateObj { get; set; } = new();

    protected object ProtectedObj { get; set; } = new();

    public string Name { get; set; } = string.Empty;

    [Column("Summ")] public string Summary { get; set; } = string.Empty;

    /// <summary>
    ///     Summary of the item.
    /// </summary>

    public string? Description { get; set; }

    public TestEnumObject Type { get; set; } = TestEnumObject.Enum1;

    #endregion

    #region Methods

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposed)
    {
        this.IsDisposed = disposed;
    }

    #endregion
}