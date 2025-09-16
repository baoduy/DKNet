namespace DKNet.RandomCreator;

public static class RandomCreators
{
    public static string String(int length = 25, bool includeSymbols = false)
    {
        using var gen = new StringCreator(length,
            includeSymbols ? StringCreator.DefaultCharsWithSymbols : StringCreator.DefaultChars);
        return gen.ToString();
    }


    public static char[] Chars(int length = 25, bool includeSymbols = false)
    {
        using var gen = new StringCreator(length,
            includeSymbols ? StringCreator.DefaultCharsWithSymbols : StringCreator.DefaultChars);
        return gen.ToChars();
    }
}