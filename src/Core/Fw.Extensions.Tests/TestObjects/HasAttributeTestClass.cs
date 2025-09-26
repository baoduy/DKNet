namespace Fw.Extensions.Tests.TestObjects;

[AttributeUsage(AttributeTargets.All)]
public sealed class TestingAttribute : Attribute;

[Testing]
public class HasAttributeTestClass1
{
    public virtual int Prop0 { get; set; }

    [Testing] public virtual string Prop1 { get; set; } = null!;
}

public class HasAttributeTestClass2 : HasAttributeTestClass1
{
    public override string Prop1
    {
        get => "AAA";
        set => base.Prop1 = value;
    }
}

public sealed class HasAttributeTestClass3
{
    public string Prop3 { get; set; } = null!;

    public object Prop4 { get; set; }= null!;

    public int Prop5 { get; set; }
}