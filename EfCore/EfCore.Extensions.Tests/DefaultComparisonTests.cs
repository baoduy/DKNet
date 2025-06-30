namespace EfCore.Extensions.Tests;

[TestClass]
public class DefaultComparisonTests
{
    [TestMethod]
    public void Test()
    {
        EqualityComparer<int>.Default.Equals(0, 0).ShouldBeTrue();
        EqualityComparer<long>.Default.Equals(0, 0).ShouldBeTrue();
    }
}