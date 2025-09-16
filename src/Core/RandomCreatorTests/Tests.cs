using DKNet.RandomCreator;
using Xunit;
using Xunit.Abstractions;

namespace RandomCreatorTests;

public class Tests(ITestOutputHelper output)
{
    [Fact]
    public void CanGenerateA25CharacterString()
    {
        var randomString = RandomCreators.String();
        output.WriteLine(randomString);

        Assert.True(randomString.Length == 25);
    }

    [Fact]
    public void CanGenerateA128CharacterString()
    {
        var randomString = RandomCreators.String(128);
        output.WriteLine(randomString);

        Assert.True(randomString.Length == 128);
    }

    [Fact]
    public void CanGenerateA128Characters()
    {
        var randomString = RandomCreators.Chars(128, true);
        output.WriteLine(new string(randomString));

        Assert.True(randomString.Length == 128);
    }

    [Fact]
    public void CanGenerateA25Characters()
    {
        var randomString = RandomCreators.Chars(includeSymbols: true);
        output.WriteLine(new string(randomString));

        Assert.True(randomString.Length == 25);
    }
}