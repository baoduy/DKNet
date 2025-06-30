using DKNet.Fw.Extensions;

namespace Fw.Extensions.Tests;

[TestClass]
public class AttributeExtensionsTestCases
{
    #region Methods

    [TestMethod]
    [TestCategory("Fw.Extensions")]
    [ExpectedException(typeof(ArgumentNullException))]
    public void GetAttributeWithNullTypeReturnNullTest()
    {
        Type type = null;
        Assert.IsNull(type.GetCustomAttribute<TestingAttribute>());
        Assert.IsNull(((PropertyInfo)null).GetCustomAttribute<TestingAttribute>());
    }

    [TestMethod]
    [TestCategory("Fw.Extensions")]
    public void GetAttributeWithTypeReturnExpectedAttributeTest()
    {
        var type = typeof(HasAttributeTestClass1);
        Assert.IsNotNull(type.GetCustomAttribute<TestingAttribute>());
        Assert.IsNotNull(type.GetCustomAttribute<TestingAttribute>() is not null);
    }

    [TestMethod]
    [TestCategory("Fw.Extensions")]
    public void GetAttributeGenericWithTypeReturnExpectedAttributeTest()
    {
        var type = typeof(HasAttributeTestClass1);
        Assert.IsNotNull(type.GetCustomAttribute<TestingAttribute>());
    }

    [TestMethod]
    [TestCategory("Fw.Extensions")]
    public void HasAttributeTest()
    {
        var obj1 = new HasAttributeTestClass1();
        Assert.IsTrue(obj1.GetType().HasAttribute<TestingAttribute>());
        Assert.IsTrue(obj1.HasAttributeOnProperty<TestingAttribute>("Prop1"));

        var obj2 = new HasAttributeTestClass2();
        Assert.IsTrue(obj2.GetType().HasAttribute<TestingAttribute>());
        Assert.IsTrue(obj2.HasAttributeOnProperty<TestingAttribute>("Prop1"));

        var obj3 = new HasAttributeTestClass3();
        Assert.IsFalse(obj3.GetType().HasAttribute<TestingAttribute>());
        Assert.IsFalse(obj3.HasAttributeOnProperty<TestingAttribute>("Prop3"));
    }

    [TestMethod]
    [TestCategory("Fw.Extensions")]
    public void NullPropertyInfoHasAttributeReturnsFalseTest()
    {
        Assert.IsFalse(((PropertyInfo)null).HasAttribute<TestingAttribute>());
    }

    #endregion Methods
}