using DKNet.EfCore.Abstractions.Entities;
using DKNet.Fw.Extensions;
using DKNet.Fw.Extensions.TypeExtractors;

namespace EfCore.Extensions.Tests;

public class TypeExtractorExtensionsTests
{
    [Fact]
    public void TestAbstract()
    {
        typeof(MyDbContext).Assembly.Extract().Abstract()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestHasAttribute()
    {
        typeof(MyDbContext).Assembly.Extract().HasAttribute<StaticDataAttribute>()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestInterface()
    {
        typeof(MyDbContext).Assembly.Extract().IsInstanceOf<BaseEntity>()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestNested()
    {
        typeof(MyDbContext).Assembly.Extract().Nested()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestNotClass()
    {
        typeof(MyDbContext).Assembly.Extract().NotClass().Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestNotEnum()
    {
        typeof(MyDbContext).Assembly.Extract().NotEnum()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void ExtractEnumsTest()
    {
        typeof(MyDbContext).Assembly.Extract().Enums().HasAttribute<StaticDataAttribute>()
            .Count().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TestExtract()
    {
        typeof(MyDbContext).Assembly.Extract().Publics().Classes().Count()
            .ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void TestIsImplementOf()
    {
        typeof(User).IsImplementOf(typeof(IEntity<>)).ShouldBeTrue();
        typeof(List<>).IsImplementOf(typeof(IEntity<>)).ShouldBeFalse();
        typeof(BaseEntity).IsImplementOf(typeof(IConcurrencyEntity<>)).ShouldBeTrue();
    }

    [Fact]
    public void TestExtractNotInstanceOf()
    {
        var list = typeof(MyDbContext).Assembly.Extract().Classes().NotInstanceOf(typeof(IEntity<>)).ToList();
        list.Contains(typeof(User)).ShouldBeFalse();
        list.Contains(typeof(Address)).ShouldBeFalse();
    }
}