namespace DKNet.RandomCreator;

internal sealed class StringCreator(int bufferLength, string? valid = StringCreator.DefaultChars )
    : IDisposable
{
    internal const string DefaultChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
    internal const string DefaultCharsWithSymbols = $"{DefaultChars}!@#$%^&*()-_=+[]{{}}|;:',.<>/?`~";
    private bool _disposed;

    private readonly Lazy<RandomByteProvider> _lazyByteProvider = new(() => new RandomByteProvider(bufferLength));

    private RandomByteProvider ByteProvider => _lazyByteProvider.Value;

    public char[] ToChars()
    {
        var v = valid ?? DefaultChars;
        return ByteProvider.Bytes().Select(b => (char)b).Where(v.Contains).Take(bufferLength).ToArray();
    }

    public override string ToString()
    {
        var chars = ToChars();
        return new string(chars);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing || _disposed) return;

        if (_lazyByteProvider.IsValueCreated)
        {
            _lazyByteProvider.Value.Dispose();
        }

        _disposed = true;
    }
}