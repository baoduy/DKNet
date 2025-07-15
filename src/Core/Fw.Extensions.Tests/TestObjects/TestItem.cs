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
    int Id { get; set; }

    string Name { get; set; }
}

public class TestItem : ITem
{
    public string Details { get; set; }

    public int Id { get; set; }

    public string Name { get; set; }
}

public class TestItem2 : ITem
{
    public int Id { get; set; }

    public string Name { get; set; }
}

public class TestItem3 : ITem, IDisposable
{
    public TestItem3()
    {
    }

    public TestItem3(string name) => Name = name;

    /// <summary>
    ///     Summary of the item.
    /// </summary>

    public string Description { get; set; }

    public bool IsDisposed { get; private set; }

    [Column("Summ")] public string Summary { get; set; }

    public TestEnumObject Type { get; set; } = TestEnumObject.Enum1;

    public int IntValue { get; set; }

    public bool BoolValue { get; set; }

    public decimal DecimalValue { get; set; }

    public int? NullableIntValue { get; set; }

    protected object ProtectedObj { get; set; } = new();

    [SuppressMessage("Style", "IDE0051:Remove unused private members", Justification = "<Pending>")]
    private object PrivateObj { get; set; } = new();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public int Id { get; set; }

    public string Name { get; set; }

    protected virtual void Dispose(bool disposed)
    {
        IsDisposed = disposed;
    }
}