namespace Svc.Transform.Tests.Convertors;


public class ConvertorTests
{
    #region Methods

    [Fact]
    public void Convertor()
    {
        var c = new ValueFormatter();

        c.Convert(token: null, value: null).ShouldBe("");
        c.Convert(token: null, 123456).ShouldBe("123,456");
        c.Convert(token: null, 1234.56m).ShouldBe("1,234.56");
        c.Convert(token: null, DateTime.Now).ShouldContain(DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture));
        c.Convert(token: null, DateTimeOffset.Now).ShouldContain(DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture));
        c.Convert(token: null, value: true).ShouldBe("Yes");
        c.Convert(token: null, 123456L).ShouldBe("123,456");
        c.Convert(token: null, 123456.78d).ShouldBe("123,456.78");
        c.Convert(token: null, (float)123456.70).ShouldBe("123,456.70");
    }

    #endregion Methods
}