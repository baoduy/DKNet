using DKNet.Fw.Extensions;

namespace Fw.Extensions.Tests;

public class AttributeExtensionsTestCases
{
    #region Methods

    [Fact]
    public void GetAttributeGenericWithTypeReturnExpectedAttributeTest()
    {
        var type = typeof(HasAttributeTestClass1);
        type.GetCustomAttribute<TestingAttribute>().ShouldNotBeNull();
    }

    [Fact]
    public void GetAttributeWithNullTypeReturnNullTest()
    {
        Type type = null!;
        Should.Throw<ArgumentNullException>(() => type.GetCustomAttribute<TestingAttribute>());
        Should.Throw<ArgumentNullException>(() => ((PropertyInfo)null!).GetCustomAttribute<TestingAttribute>());
    }

    [Fact]
    public void GetAttributeWithTypeReturnExpectedAttributeTest()
    {
        var type = typeof(HasAttributeTestClass1);
        type.GetCustomAttribute<TestingAttribute>().ShouldNotBeNull();
        (type.GetCustomAttribute<TestingAttribute>() is not null).ShouldBeTrue();
    }

    [Fact]
    public void HasAttributeTest()
    {
        var obj1 = new HasAttributeTestClass1();
        obj1.GetType().HasAttribute<TestingAttribute>().ShouldBeTrue();
        obj1.HasAttributeOnProperty<TestingAttribute>("Prop1").ShouldBeTrue();

        var obj2 = new HasAttributeTestClass2();
        obj2.GetType().HasAttribute<TestingAttribute>().ShouldBeTrue();
        obj2.HasAttributeOnProperty<TestingAttribute>("Prop1").ShouldBeTrue();

        var obj3 = new HasAttributeTestClass3();
        obj3.GetType().HasAttribute<TestingAttribute>().ShouldBeFalse();
        obj3.HasAttributeOnProperty<TestingAttribute>("Prop3").ShouldBeFalse();
    }

    [Fact]
    public void NullPropertyInfoHasAttributeReturnsFalseTest()
    {
        ((PropertyInfo)null!).HasAttribute<TestingAttribute>().ShouldBeFalse();
    }

    #endregion
}