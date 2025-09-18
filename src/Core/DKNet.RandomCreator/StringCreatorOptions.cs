namespace DKNet.RandomCreator;

/// <summary>
/// Options for configuring the StringCreator random string generator.
/// </summary>
public sealed class StringCreatorOptions
{
    /// <summary>
    /// If true, only alphabetic characters will be used.
    /// </summary>
    //public bool AlphabeticOnly { get; set; }

    /// <summary>
    /// Minimum number of numeric characters required.
    /// </summary>
    public int MinNumbers { get; set; }

    /// <summary>
    /// Minimum number of special characters required.
    /// </summary>
    public int MinSpecials { get; set; }
}