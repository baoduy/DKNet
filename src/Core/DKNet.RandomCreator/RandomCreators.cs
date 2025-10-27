namespace DKNet.RandomCreator;

public static class RandomCreators
{
    #region Methods

    public static char[] NewChars(int length = 25, StringCreatorOptions? options = null)
    {
        using var gen = new StringCreator(length,
            options ?? new StringCreatorOptions());

        return gen.ToChars();
    }

    public static string NewString(int length = 25, StringCreatorOptions? options = null)
    {
        using var gen = new StringCreator(length,
            options ?? new StringCreatorOptions());
        return gen.ToString();
    }

    #endregion
}