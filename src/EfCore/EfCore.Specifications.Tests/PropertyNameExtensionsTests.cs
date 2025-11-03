namespace EfCore.Specifications.Tests;

public class PropertyNameExtensionsTests
{
    #region Methods

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("foo", "Foo")]
    [InlineData("fooBar", "FooBar")]
    [InlineData("foo_bar", "FooBar")]
    [InlineData("foo-bar", "FooBar")]
    [InlineData("foo_bar-baz", "FooBarBaz")]
    [InlineData("FOO_BAR", "FOOBAR")]
    [InlineData("foo1_bar2", "Foo1Bar2")]
    [InlineData("_foo_", "Foo")]
    [InlineData("foo__bar", "FooBar")]
    [InlineData("foo-bar_baz", "FooBarBaz")]
    [InlineData("f", "F")]
    [InlineData("f_b", "FB")]
    public void ToPascalCase_WorksAsExpected(string input, string expected)
    {
        var result = input.ToPascalCase();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("foo", "Foo")]
    [InlineData("foo.bar", "Foo.Bar")]
    [InlineData("foo_bar.baz_qux", "FooBar.BazQux")]
    [InlineData("foo-bar.baz-qux", "FooBar.BazQux")]
    [InlineData("foo_bar-baz.qux_foo", "FooBarBaz.QuxFoo")]
    [InlineData("foo.bar.baz", "Foo.Bar.Baz")]
    [InlineData("foo1.bar2", "Foo1.Bar2")]
    [InlineData("_foo_._bar_", "Foo.Bar")]
    [InlineData("foo..bar", "Foo.Bar")]
    [InlineData("foo-bar_baz.qux", "FooBarBaz.Qux")]
    [InlineData("f.b", "F.B")]
    public void ToPascalCasePath_WorksAsExpected(string input, string expected)
    {
        var result = input.ToPascalCase();
        Assert.Equal(expected, result);
    }

    #endregion
}