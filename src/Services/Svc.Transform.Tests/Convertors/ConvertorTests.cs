namespace Svc.Transform.Tests.Convertors;

public class ConvertorTests
{
    #region Methods

    [Fact]
    public void Convertor()
    {
        var c = new ValueFormatter();

        c.Convert(null!, null).ShouldBe("");
        c.Convert(null!, 123456).ShouldBe("123,456");
        c.Convert(null!, 1234.56m).ShouldBe("1,234.56");
        c.Convert(null!, DateTime.Now).ShouldContain(DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture));
        c.Convert(null!, DateTimeOffset.Now)
            .ShouldContain(DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture));
        c.Convert(null!, true).ShouldBe("Yes");
        c.Convert(null!, 123456L).ShouldBe("123,456");
        c.Convert(null!, 123456.78d).ShouldBe("123,456.78");
        c.Convert(null!, (float)123456.70).ShouldBe("123,456.70");
    }

    #endregion
}