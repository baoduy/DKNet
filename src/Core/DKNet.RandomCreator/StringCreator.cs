using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace DKNet.RandomCreator;

[SuppressMessage("Security", "CA5394:Do not use insecure randomness")]
internal sealed class StringCreator(int bufferLength, StringCreatorOptions options) : IDisposable
{
    #region Fields

    private readonly RandomNumberGenerator _cryptoGen = RandomNumberGenerator.Create();
    private bool _disposed;

    #endregion

    #region Methods

    public void Dispose()
    {
        this._cryptoGen.Dispose();
        this._disposed = true;
    }

    private char[] Generate(string validChars, int length)
    {
        ObjectDisposedException.ThrowIf(this._disposed, nameof(StringCreator));

        var buffer = new byte[length * 8];
        this._cryptoGen.GetBytes(buffer);
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            var rnd = BitConverter.ToUInt64(buffer, i * 8);
            result[i] = validChars[(int)(rnd % (uint)validChars.Length)];
        }

        return result;
    }

    public char[] ToChars()
    {
        // Prepare result list
        if (bufferLength <= 0)
        {
            throw new ArgumentException("Length must be greater than zero.", nameof(bufferLength));
        }

        if (options.MinNumbers + options.MinSpecials >= bufferLength)
        {
            throw new ArgumentException(
                "The sum of MinNumbers and MinSpecials must be less than the total length.",
                nameof(options));
        }

        var result = new List<char>(bufferLength);

        // Add minimum numbers
        result.AddRange(
            options.MinNumbers <= 0
                ? []
                : this.Generate(DefaultNumbers, options.MinNumbers));

        // Add minimum specials
        result.AddRange(
            options.MinSpecials <= 0
                ? []
                : this.Generate(DefaultSymbols, options.MinSpecials));

        // Fill the rest
        var remaining = bufferLength - result.Count;
        result.AddRange(this.Generate(DefaultChars, remaining));

        var array = result.ToArray();
        new Random().Shuffle(array);
        return array;
    }

    public override string ToString()
    {
        var chars = this.ToChars();
        return new string(chars);
    }

    #endregion

    private const string DefaultChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string DefaultNumbers = "1234567890";
    private const string DefaultSymbols = "!@#$%^&*()-_=+[]{{}}|;:',.<>/?`~";
}