namespace Fw.Extensions.Tests.TestObjects;

[AttributeUsage(AttributeTargets.All)]
public sealed class TestingAttribute : Attribute;

[Testing]
public class HasAttributeTestClass1
{
    #region Properties

    public virtual int Prop0 { get; set; }

    [Testing] public virtual string Prop1 { get; set; } = null!;

    #endregion
}

public class HasAttributeTestClass2 : HasAttributeTestClass1
{
    #region Properties

    public override string Prop1
    {
        get => "AAA";
        set => base.Prop1 = value;
    }

    #endregion
}

public sealed class HasAttributeTestClass3
{
    #region Properties

    public string Prop3 { get; set; } = null!;

    public object Prop4 { get; set; } = null!;

    public int Prop5 { get; set; }

    #endregion
}