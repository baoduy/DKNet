using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.Fw.Extensions.TypeExtractors;

namespace EfCore.Extensions.Tests;

[TestClass]
public class TypeExtractorExtensionsTests
{

    [TestMethod]
    public void TestAbstract()
    {
        typeof(MyDbContext).Assembly.Extract().Abstract()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestHasAttribute()
    {
        typeof(MyDbContext).Assembly.Extract().HasAttribute<StaticDataAttribute>()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestInterface()
    {
        typeof(MyDbContext).Assembly.Extract().IsInstanceOf<BaseEntity>()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestNested()
    {
        typeof(MyDbContext).Assembly.Extract().Nested()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestNotClass()
    {
        typeof(MyDbContext).Assembly.Extract().NotClass().
            Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestNotEnum()
    {
        typeof(MyDbContext).Assembly.Extract().NotEnum()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void ExtractEnumsTest()
    {
        typeof(MyDbContext).Assembly.Extract().Enums().HasAttribute<StaticDataAttribute>()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void TestExtract()
    {
        typeof(MyDbContext).Assembly.Extract().Publics().Classes().Count()
            .ShouldBeGreaterThanOrEqualTo(3);
    }



    [TestMethod]
    public void TestExtractNotInstanceOf()
    {
        var list = typeof(MyDbContext).Assembly.Extract().Classes().NotInstanceOf(typeof(IEntity<>)).ToList();
        list.Contains(typeof(User)).ShouldBeFalse();
        list.Contains(typeof(Address)).ShouldBeFalse();
    }
}