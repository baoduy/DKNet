using DKNet.RandomCreator;
using Shouldly;
using Xunit;

namespace RandomCreatorTests;

/// <summary>
///     Frequency-based regression coverage for <see cref="StringCreator" />'s symbol pool.
///     The real defect this guards against: the special-character pool used to have 32 slots for
///     only 30 distinct characters (two braces duplicated), so those two characters were drawn
///     twice as often as every other special character. A modulo-bias claim was investigated and
///     withdrawn; frequency is the property that actually regresses.
/// </summary>
public class UniformityTests
{
    #region Methods

    [Fact]
    public void NewChars_TwentyThousandRunsWithSevenSpecialsEach_EachSpecialCharFrequencyStaysWithinUniformTolerance()
    {
        // Arrange
        var options = new StringCreatorOptions { MinNumbers = 0, MinSpecials = 7 };
        var frequencies = new Dictionary<char, int>();

        // Act: each run of length 8 with MinSpecials=7 yields exactly 7 non-alphanumeric chars.
        for (var i = 0; i < 20_000; i++)
        {
            var chars = RandomCreators.NewChars(8, options);
            foreach (var c in chars)
            {
                if (char.IsLetterOrDigit(c)) continue;
                frequencies[c] = frequencies.GetValueOrDefault(c) + 1;
            }
        }

        // Assert: with the fixed 30-distinct-character pool every symbol should be drawn with
        // roughly equal frequency. On the old 32-slot pool (two braces duplicated) the duplicated
        // characters would be drawn ~2x as often, blowing well past this tolerance.
        frequencies.Count.ShouldBe(30, "expected exactly 30 distinct special characters to appear");
        var min = frequencies.Values.Min();
        var max = frequencies.Values.Max();
        ((double)max / min).ShouldBeLessThan(1.3,
            $"special character frequencies are not uniform: min={min}, max={max}, distribution={string.Join(", ", frequencies.Select(kv => $"'{kv.Key}'={kv.Value}"))}");
    }

    [Fact]
    public void NewChars_TwentyThousandAlphabeticRunsOfLengthEight_EachLetterFrequencyStaysWithinUniformTolerance()
    {
        // Arrange
        var options = new StringCreatorOptions { MinNumbers = 0, MinSpecials = 0 };
        var frequencies = new Dictionary<char, int>();

        // Act
        for (var i = 0; i < 20_000; i++)
        {
            var chars = RandomCreators.NewChars(8, options);
            foreach (var c in chars) frequencies[c] = frequencies.GetValueOrDefault(c) + 1;
        }

        // Assert
        frequencies.Count.ShouldBe(52, "expected all 26 lower + 26 upper letters to appear");
        var min = frequencies.Values.Min();
        var max = frequencies.Values.Max();
        ((double)max / min).ShouldBeLessThan(1.3,
            $"letter frequencies are not uniform: min={min}, max={max}");
    }

    #endregion
}
