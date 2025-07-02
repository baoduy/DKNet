using DKNet.Fw.Extensions;

namespace Fw.Extensions.Tests;

public class StringExtensionsTests
{
    [Fact]
    public void IsNumberTests()
    {
        "123".IsNumber().ShouldBeTrue();
        "123,456".IsNumber().ShouldBeTrue();
        "123.456".IsNumber().ShouldBeTrue();
        "123,A.456".IsNumber().ShouldBeFalse();
        "-123.456".IsNumber().ShouldBeTrue();
        "123.456.789".IsNumber().ShouldBeFalse();
        "123,456,789".IsNumber().ShouldBeTrue();
        "123,456.789".IsNumber().ShouldBeTrue();
        "123.456,789".IsNumber().ShouldBeTrue();
        "123.456.789.012".IsNumber().ShouldBeFalse();
        "123,456,789,012".IsNumber().ShouldBeTrue();
        "-123,456,789".IsNumber().ShouldBeTrue();
        "-123.456".IsNumber().ShouldBeTrue();
        "123-456".IsNumber().ShouldBeFalse();
        "123.456-789".IsNumber().ShouldBeFalse();
        "123,456-789".IsNumber().ShouldBeFalse();
        "123.456,789".IsNumber().ShouldBeTrue();
        "123,456.789".IsNumber().ShouldBeTrue();
        "123.456.789".IsNumber().ShouldBeFalse();
        "123,456,789".IsNumber().ShouldBeTrue();
        "123,456-789".IsNumber().ShouldBeFalse();
        "123.456-789".IsNumber().ShouldBeFalse();
        "-123,456,789".IsNumber().ShouldBeTrue();
        "-123.456".IsNumber().ShouldBeTrue();
        "".IsNumber().ShouldBeFalse();
        " ".IsNumber().ShouldBeFalse();
        ((string)null).IsNumber().ShouldBeFalse();
    }

    [Fact]
    public void ExtractNumberTests()
    {
        "123abc".ExtractDigits().ShouldBe("123");
        "abc123.456xyz".ExtractDigits().ShouldBe("123.456");
        "abc123,456xyz".ExtractDigits().ShouldBe("123,456");
        "abc-123.456xyz".ExtractDigits().ShouldBe("-123.456");
        "abc123,456-789xyz".ExtractDigits().ShouldBe("123,456-789");
        "abc123.456,789xyz".ExtractDigits().ShouldBe("123.456,789");
        "abc123,456.789xyz".ExtractDigits().ShouldBe("123,456.789");
        "abc123.456.789xyz".ExtractDigits().ShouldBe("123.456.789");
        "abc123,456,789xyz".ExtractDigits().ShouldBe("123,456,789");
        "abc123,456-789xyz".ExtractDigits().ShouldBe("123,456-789");
        "abc123.456-789xyz".ExtractDigits().ShouldBe("123.456-789");
        "abc-123,456,789xyz".ExtractDigits().ShouldBe("-123,456,789");
        "abc-123.456xyz".ExtractDigits().ShouldBe("-123.456");
        "".ExtractDigits().ShouldBe("");
        " ".ExtractDigits().ShouldBe("");
        Action action = () => ((string)null).ExtractDigits();
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void IsStringOrValueTypeTests()
    {
        typeof(string).IsStringOrValueType().ShouldBeTrue();
        typeof(int).IsStringOrValueType().ShouldBeTrue();
        typeof(long).IsStringOrValueType().ShouldBeTrue();
        typeof(double).IsStringOrValueType().ShouldBeTrue();
        typeof(decimal).IsStringOrValueType().ShouldBeTrue();
        typeof(byte).IsStringOrValueType().ShouldBeTrue();

        typeof(int?).IsStringOrValueType().ShouldBeTrue();
        typeof(long?).IsStringOrValueType().ShouldBeTrue();
        typeof(double?).IsStringOrValueType().ShouldBeTrue();
        typeof(decimal?).IsStringOrValueType().ShouldBeTrue();
        typeof(byte?).IsStringOrValueType().ShouldBeTrue();

        typeof(object).IsStringOrValueType().ShouldBeFalse();
        typeof(FactAttribute).IsStringOrValueType().ShouldBeFalse();
        typeof(string[]).IsStringOrValueType().ShouldBeFalse();
        typeof(int[]).IsStringOrValueType().ShouldBeFalse();
        typeof(List<int>).IsStringOrValueType().ShouldBeFalse();
        typeof(Dictionary<string, int>).IsStringOrValueType().ShouldBeFalse();
    }

    [Fact]
    public void IsStringOrValueTypeNullTests()
    {
        ((Type)null).IsStringOrValueType().ShouldBeFalse();
        ((PropertyInfo)null).IsStringOrValueType().ShouldBeFalse();
    }

    [Fact]
    public void IsStringOrValueTypeNullableTypesTests()
    {
        typeof(int?).IsStringOrValueType().ShouldBeTrue();
        typeof(long?).IsStringOrValueType().ShouldBeTrue();
        typeof(double?).IsStringOrValueType().ShouldBeTrue();
        typeof(decimal?).IsStringOrValueType().ShouldBeTrue();
        typeof(byte?).IsStringOrValueType().ShouldBeTrue();
    }
}