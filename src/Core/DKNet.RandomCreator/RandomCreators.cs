namespace DKNet.RandomCreator;

public static class RandomCreators
{
    public static string NewString(int length = 25, StringCreatorOptions? options = null)
    {
        using var gen = new StringCreator(length,
            options ?? new StringCreatorOptions());
        return gen.ToString();
    }

    public static char[] NewChars(int length = 25, StringCreatorOptions? options = null)
    {
        using var gen = new StringCreator(length,
            options ?? new StringCreatorOptions());

        return gen.ToChars();
    }
}