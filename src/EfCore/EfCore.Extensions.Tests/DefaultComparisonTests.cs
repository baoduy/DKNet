namespace EfCore.Extensions.Tests;


public class DefaultComparisonTests
{
    [Fact]
    public void Test()
    {
        EqualityComparer<int>.Default.Equals(0, 0).ShouldBeTrue();
        EqualityComparer<long>.Default.Equals(0, 0).ShouldBeTrue();
    }
}