using System.Security.Cryptography;

namespace DKNet.RandomCreator;

internal sealed class RandomByteProvider : IDisposable
{
    private bool _disposed;
    private readonly IEnumerator<byte> _enumerator;
    private readonly object _locker = new();
    private readonly int _bufferLength;
    private readonly RandomNumberGenerator _cryptoGen = RandomNumberGenerator.Create();

    public RandomByteProvider(int bufferLength)
    {
        _bufferLength = bufferLength;
        _enumerator = GetBytes().GetEnumerator();
    }

    public IEnumerable<int> Bytes()
    {
        while(true)
        {
            // This locking doesn't actually make it 100% threadsafe - at least not in theory
            // But it does somehow seem to work...
            lock(_locker)
            {
                if (_enumerator.MoveNext())
                {
                    yield return _enumerator.Current;
                }
                else
                {
                    break;
                }
            }
        }
    }

    private IEnumerable<byte> GetBytes()
    {
        //
        // TODO utilise IDisposable
        // Gives BCryptGenRandom on Windows and
        var bytes = new byte[_bufferLength];

        while (true)
        {
            // Console.WriteLine("** Asking cryptogen for more bytes **");
            _cryptoGen.GetBytes(bytes);
            for (int i = 0; i < _bufferLength; i++)
            {
                //Debug.WriteLine($"Returning byte {i}");
                yield return bytes[i];
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _cryptoGen.Dispose();
        _disposed = true;
    }
}