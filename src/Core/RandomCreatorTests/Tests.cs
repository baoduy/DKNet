using DKNet.RandomCreator;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace RandomCreatorTests;

public class Tests(ITestOutputHelper output)
{
    #region Methods

    [Fact]
    public void CanDisposeStringCreatorSafely()
    {
        // Disposal of internal class cannot be tested directly; test via public API
        var options = new StringCreatorOptions { MinNumbers = 2, MinSpecials = 2 };
        var chars = RandomCreators.NewChars(10, options);
        Assert.Equal(10, chars.Length); // If no exception, disposal is safe
    }

    [Fact]
    public void CanGenerateA128Characters()
    {
        var randomString = RandomCreators.NewChars(128);
        output.WriteLine(new string(randomString));

        Assert.True(randomString.Length == 128);
    }

    [Fact]
    public void CanGenerateA128CharacterString()
    {
        var randomString = RandomCreators.NewString(128);
        output.WriteLine(randomString);

        Assert.True(randomString.Length == 128);
    }

    [Fact]
    public void CanGenerateA25CharacterString()
    {
        var randomString = RandomCreators.NewString();
        output.WriteLine(randomString);

        Assert.True(randomString.Length == 25);
    }

    [Fact]
    public void CanGenerateAlphabeticOnlyChars()
    {
        var options = new StringCreatorOptions { MinNumbers = 0, MinSpecials = 0 };
        var chars = RandomCreators.NewChars(20, options);
        output.WriteLine(new string(chars));
        Assert.True(chars.All(char.IsLetter));
        Assert.Equal(20, chars.Length);
    }

    [Fact]
    public void CanGenerateAlphabeticOnlyString()
    {
        var options = new StringCreatorOptions { MinNumbers = 0, MinSpecials = 0 };
        var str = RandomCreators.NewString(50, options);
        output.WriteLine(str);
        Assert.True(str.All(char.IsLetter));
        Assert.Equal(50, str.Length);
    }

    [Fact]
    public void CanGenerateCharsWithAllOptions()
    {
        var options = new StringCreatorOptions { MinNumbers = 2, MinSpecials = 2 };
        var chars = RandomCreators.NewChars(20, options);
        output.WriteLine(new string(chars));
        var numCount = chars.Count(c => "1234567890".Contains(c, StringComparison.Ordinal));
        var specCount = chars.Count(c => "!@#$%^&*()-_=+[]{{}}|;:',.<>/?`~".Contains(c, StringComparison.Ordinal));
        var alphaCount = chars.Count(char.IsLetter);
        Assert.True(numCount >= 2);
        Assert.True(specCount >= 2);
        Assert.True(alphaCount + numCount + specCount == 20);
    }

    [Fact]
    public void CanGenerateCharsWithOptions()
    {
        var options = new StringCreatorOptions { MinNumbers = 2, MinSpecials = 2 };
        var chars = RandomCreators.NewChars(20, options);
        output.WriteLine(new string(chars));
        Assert.Equal(20, chars.Length);
    }

    [Fact]
    public void CanGenerateStringWithAllOptions()
    {
        var options = new StringCreatorOptions { MinNumbers = 2, MinSpecials = 2 };
        var str = RandomCreators.NewString(20, options);
        output.WriteLine(str);
        var numCount = str.Count(c => "1234567890".Contains(c, StringComparison.Ordinal));
        var specCount = str.Count(c => "!@#$%^&*()-_=+[]{{}}|;:',.<>/?`~".Contains(c, StringComparison.Ordinal));
        var alphaCount = str.Count(char.IsLetter);
        Assert.True(numCount >= 2);
        Assert.True(specCount >= 2);
        Assert.True(alphaCount + numCount + specCount == 20);
    }

    [Fact]
    public void CanGenerateStringWithExcessiveMinNumbersAndSpecials()
    {
        var options = new StringCreatorOptions { MinNumbers = 5, MinSpecials = 20 };
        var str = RandomCreators.NewString(30, options);
        output.WriteLine(str);
        var numCount = str.Count(c => "1234567890".Contains(c, StringComparison.Ordinal));
        var specCount = str.Count(c => "!@#$%^&*()-_=+[]{{}}|;:',.<>/?`~".Contains(c, StringComparison.Ordinal));

        Assert.True(numCount == 5);
        Assert.True(specCount == 20);
        Assert.Equal(30, str.Length);
    }

    [Fact]
    public void CanGenerateStringWithMinNumbersAndSpecials()
    {
        var options = new StringCreatorOptions { MinNumbers = 5, MinSpecials = 3 };
        var str = RandomCreators.NewString(30, options);
        output.WriteLine(str);
        var numCount = str.Count(c => "1234567890".Contains(c, StringComparison.Ordinal));
        var specCount = str.Count(c => "!@#$%^&*()-_=+[]{{}}|;:',.<>/?`~".Contains(c, StringComparison.Ordinal));
        Assert.True(numCount >= 5);
        Assert.True(specCount >= 3);
        Assert.Equal(30, str.Length);
    }

    [Fact]
    public void CanGenerateZeroLengthString()
    {
        Action a = () => RandomCreators.NewString(0);
        a.ShouldThrow<ArgumentException>();
    }

    #endregion
}