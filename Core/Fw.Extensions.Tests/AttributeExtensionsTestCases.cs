using System.Reflection;
using DKNet.Fw.Extensions;
using Fw.Extensions.Tests.TestObjects;

namespace Fw.Extensions.Tests;

[TestClass]
public class AttributeExtensionsTestCases
{
    #region Methods

    // [TestMethod]
    // [TestCategory("Fw.Extensions")]
    // public void GetAttribute()
    // {
    //     Assert.IsTrue(this.GetAttribute<TestClassAttribute>() != null);
    // }

    [TestMethod]
    [TestCategory("Fw.Extensions")]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetAttributeWithNullTypeReturnNullTest()
    {
        Type type = null;
        Assert.IsNull(type.GetCustomAttribute<TestAttribute>());
        Assert.IsNull(((PropertyInfo)null).GetCustomAttribute<TestAttribute>());
    }

    [TestMethod]
    [TestCategory("Fw.Extensions")]
    public void GetAttributeWithTypeReturnExpectedAttributeTest()
    {
        var type = typeof(HasAttributeTestClass1);
        Assert.IsNotNull(type.GetCustomAttribute<TestAttribute>());
        Assert.IsNotNull(type.GetCustomAttribute<TestAttribute>() is not null);
    }

    [TestMethod]
    [TestCategory("Fw.Extensions")]
    public void GetAttributeGenericWithTypeReturnExpectedAttributeTest()
    {
        var type = typeof(HasAttributeTestClass1);
        Assert.IsNotNull(type.GetCustomAttribute<TestAttribute>());
    }

    [TestMethod]
    [TestCategory("Fw.Extensions")]
    public void HasAttributeTest()
    {
        var obj1 = new HasAttributeTestClass1();
        Assert.IsTrue(obj1.GetType().HasAttribute<TestAttribute>());
        Assert.IsTrue(obj1.HasAttributeOnProperty<TestAttribute>("Prop1"));

        var obj2 = new HasAttributeTestClass2();
        Assert.IsTrue(obj2.GetType().HasAttribute<TestAttribute>());
        Assert.IsTrue(obj2.HasAttributeOnProperty<TestAttribute>("Prop1"));

        var obj3 = new HasAttributeTestClass3();
        Assert.IsFalse(obj3.GetType().HasAttribute<TestAttribute>());
        Assert.IsFalse(obj3.HasAttributeOnProperty<TestAttribute>("Prop3"));
    }

    [TestMethod]
    [TestCategory("Fw.Extensions")]
    public void NullPropertyInfoHasAttributeReturnsFalseTest()
    {
        Assert.IsFalse(((PropertyInfo)null).HasAttribute<TestAttribute>());
    }

    #endregion Methods
}