// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: SizeLimitedStream.cs
// Description: A read-only stream wrapper that enforces a byte ceiling while it is read, for sources whose
//              length cannot be known upfront (Stream.CanSeek is false).

namespace DKNet.Svc.BlobStorage.Abstractions;

/// <summary>
///     Wraps a non-seekable source <see cref="Stream" /> and throws <see cref="FileLoadException" /> once more than
///     <paramref name="maxLength" /> bytes have been read from it. Used by <see cref="BlobService" /> to enforce
///     <see cref="BlobServiceOptions.MaxFileSizeInMb" /> on streams whose <see cref="Stream.Length" /> cannot be
///     checked upfront, without buffering the source to measure it first.
/// </summary>
/// <param name="source">The source stream to read from. Not owned — disposing this wrapper does not dispose it.</param>
/// <param name="maxLength">The maximum number of bytes allowed to be read before <see cref="FileLoadException" /> is thrown.</param>
internal sealed class SizeLimitedStream(Stream source, long maxLength) : Stream
{
    #region Fields

    private long _totalRead;

    #endregion

    #region Properties

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public override void Flush()
    {
        // Read-only wrapper; nothing to flush.
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        _totalRead += read;
        if (_totalRead > maxLength) throw new FileLoadException("File size is invalid.");
        return read;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    #endregion
}
