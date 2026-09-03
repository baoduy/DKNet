// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: BlobServiceStreamTests.cs
// Description: Integration tests for the stream-based OpenReadAsync/SaveAsync members across providers.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Svc.BlobStorage.Tests.Fixtures;
using DKNet.Svc.BlobStorage.Local;

namespace Svc.BlobStorage.Tests;

public class BlobServiceStreamTests
{
    #region LocalBlobService Tests

    [Fact]
    public async Task Local_SaveAsync_Stream_RoundTrip_ShouldMatchContent()
    {
        using var fixture = new LocalBlobServiceFixture();
        var content = "Local stream round trip"u8.ToArray();
        var name = $"stream-{Guid.NewGuid()}.txt";

        await fixture.Service.SaveAsync(new BlobDetails.BlobStreamData(name, new MemoryStream(content)));

        await using var read = await fixture.Service.OpenReadAsync(new BlobRequest(name));
        read.ShouldNotBeNull();
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);
        buffer.ToArray().ShouldBe(content);
    }

    [Fact]
    public async Task Local_OpenReadAsync_ReturnsFileStream_NotFullyBufferedIntoMemory()
    {
        // Proof of non-buffering: File.OpenRead returns a FileStream, which reads lazily from disk in
        // chunks rather than materializing the whole file upfront — unlike a MemoryStream copy.
        using var fixture = new LocalBlobServiceFixture();
        var content = new byte[5 * 1024 * 1024];
        Random.Shared.NextBytes(content);
        var name = $"stream-{Guid.NewGuid()}.txt";
        await fixture.Service.SaveAsync(new BlobDetails.BlobStreamData(name, new MemoryStream(content)));

        await using var read = await fixture.Service.OpenReadAsync(new BlobRequest(name));

        read.ShouldNotBeNull();
        read.ShouldBeOfType<FileStream>();

        // Reading only a prefix works without having to consume the rest.
        var prefix = new byte[16];
        var readCount = await read.ReadAsync(prefix);
        readCount.ShouldBe(16);
        prefix.ShouldBe(content[..16]);
    }

    [Fact]
    public async Task Local_OpenReadAsync_NonExistentBlob_ShouldReturnNull()
    {
        using var fixture = new LocalBlobServiceFixture();

        var result = await fixture.Service.OpenReadAsync(new BlobRequest("does-not-exist.txt"));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Local_SaveAsync_Stream_NonSeekableOversized_ShouldThrowFileLoadException()
    {
        using var fixture = new LocalBlobServiceFixture();
        var options = new LocalDirectoryOptions { RootFolder = fixture.TestRoot, MaxFileSizeInMb = 1 };
        var service = new LocalBlobService(Options.Create(options), NullLogger<LocalBlobService>.Instance);
        var oversized = new NonSeekableMemoryStream(new byte[2 * 1_000_000]);

        var exception = await Should.ThrowAsync<FileLoadException>(() =>
            service.SaveAsync(new BlobDetails.BlobStreamData("big.txt", oversized)));
        exception.Message.ShouldBe("File size is invalid.");
    }

    #endregion

    #region S3BlobService Tests

    [Fact]
    public async Task S3_SaveAsync_Stream_RoundTrip_ShouldMatchContent()
    {
        using var fixture = new S3BlobServiceFixture();
        var content = "S3 stream round trip"u8.ToArray();
        var name = $"stream-{Guid.NewGuid()}.txt";

        await fixture.Service.SaveAsync(
            new BlobDetails.BlobStreamData(name, new MemoryStream(content)) { Overwrite = true });

        await using var read = await fixture.Service.OpenReadAsync(new BlobRequest(name));
        read.ShouldNotBeNull();
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);
        buffer.ToArray().ShouldBe(content);
    }

    [Fact]
    public async Task S3_OpenReadAsync_ReturnsLiveResponseStream_NotAMemoryStream()
    {
        // The AWS SDK's GetObjectResponse.ResponseStream is the live HTTP response body — asserting it is
        // not a MemoryStream shows the object was not fully materialized before being handed back.
        using var fixture = new S3BlobServiceFixture();
        var content = new byte[512 * 1024];
        Random.Shared.NextBytes(content);
        var name = $"stream-{Guid.NewGuid()}.txt";
        await fixture.Service.SaveAsync(
            new BlobDetails.BlobStreamData(name, new MemoryStream(content)) { Overwrite = true });

        await using var read = await fixture.Service.OpenReadAsync(new BlobRequest(name));

        read.ShouldNotBeNull();
        read.ShouldNotBeOfType<MemoryStream>();
    }

    [Fact]
    public async Task S3_OpenReadAsync_NonExistentBlob_ShouldReturnNull()
    {
        using var fixture = new S3BlobServiceFixture();

        var result = await fixture.Service.OpenReadAsync(new BlobRequest($"missing-{Guid.NewGuid()}.txt"));

        result.ShouldBeNull();
    }

    #endregion

    #region AzureStorageBlobService Tests

    [Fact]
    public async Task Azure_SaveAsync_Stream_RoundTrip_ShouldMatchContent()
    {
        using var fixture = new AzureStorageBlobServiceFixture();
        var content = "Azure stream round trip"u8.ToArray();
        var name = $"stream-{Guid.NewGuid()}.txt";

        await fixture.Service.SaveAsync(
            new BlobDetails.BlobStreamData(name, new MemoryStream(content)) { Overwrite = true });

        await using var read = await fixture.Service.OpenReadAsync(new BlobRequest(name));
        read.ShouldNotBeNull();
        using var buffer = new MemoryStream();
        await read.CopyToAsync(buffer);
        buffer.ToArray().ShouldBe(content);
    }

    [Fact]
    public async Task Azure_OpenReadAsync_ReturnsLazyStream_NotAMemoryStream()
    {
        // BlobClient.OpenReadAsync returns Azure's internal lazily-chunked download stream, not a fully
        // buffered one — asserting it is not a MemoryStream is the honest signal available without
        // instrumenting the SDK's internal HTTP chunking.
        using var fixture = new AzureStorageBlobServiceFixture();
        var content = new byte[512 * 1024];
        Random.Shared.NextBytes(content);
        var name = $"stream-{Guid.NewGuid()}.txt";
        await fixture.Service.SaveAsync(
            new BlobDetails.BlobStreamData(name, new MemoryStream(content)) { Overwrite = true });

        await using var read = await fixture.Service.OpenReadAsync(new BlobRequest(name));

        read.ShouldNotBeNull();
        read.ShouldNotBeOfType<MemoryStream>();

        var prefix = new byte[16];
        var readCount = await read.ReadAsync(prefix);
        readCount.ShouldBe(16);
        prefix.ShouldBe(content[..16]);
    }

    [Fact]
    public async Task Azure_OpenReadAsync_NonExistentBlob_ShouldReturnNull()
    {
        using var fixture = new AzureStorageBlobServiceFixture();

        var result = await fixture.Service.OpenReadAsync(new BlobRequest($"missing-{Guid.NewGuid()}.txt"));

        result.ShouldBeNull();
    }

    #endregion

    /// <summary>
    ///     A <see cref="MemoryStream" />-backed stream that reports <see cref="CanSeek" /> as <c>false</c>, to
    ///     exercise the non-seekable size-enforcement path against a real <c>BlobService</c> provider the way a
    ///     live network response stream would behave.
    /// </summary>
    private sealed class NonSeekableMemoryStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
