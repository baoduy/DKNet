// <copyright file="LocalBlobServiceEdgeCaseTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.Svc.BlobStorage.Local;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Svc.BlobStorage.Tests;

/// <summary>
///     Edge-case tests for <see cref="LocalBlobService" /> that exercise the security-sensitive
///     and defensive branches not covered by the happy-path <c>LocalBlobStorageTest</c>:
///     null <c>RootFolder</c> fallback, path-traversal protection, leading-slash normalization,
///     and cancellation-token early-exits for file/directory deletion.
/// </summary>
public class LocalBlobServiceEdgeCaseTests : IDisposable
{
    #region Fields

    private readonly string _root;
    private readonly LocalBlobService _service;

    #endregion

    #region Constructors

    public LocalBlobServiceEdgeCaseTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "DKNet-LocalBlobEdge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        var options = Options.Create(new LocalDirectoryOptions { RootFolder = _root });
        _service = new LocalBlobService(options, NullLogger<LocalBlobService>.Instance);
    }

    #endregion

    #region Methods

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_NullRootFolder_FallsBackToCurrentDirectoryLocalStore()
    {
        // Arrange: options with RootFolder = null should fall back to "{cwd}/LocalStore"
        var options = Options.Create(new LocalDirectoryOptions { RootFolder = null });

        // Act
        var service = new LocalBlobService(options, NullLogger<LocalBlobService>.Instance);

        // Assert: not throwing during construction is the contract; usage should resolve relative
        // to current directory + LocalStore. We verify behavior indirectly via path traversal guard.
        Should.Throw<UnauthorizedAccessException>(() =>
            service.CheckExistsAsync(new BlobRequest("../../../etc/passwd") { Type = BlobTypes.File })
                .GetAwaiter().GetResult());
    }

    [Fact]
    public async Task CheckExistsAsync_PathTraversal_ThrowsUnauthorizedAccess()
    {
        var attempt = new BlobRequest("../../../etc/passwd") { Type = BlobTypes.File };

        var ex = await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await _service.CheckExistsAsync(attempt));
        ex.Message.ShouldContain("outside the configured root directory");
    }

    [Fact]
    public async Task DeleteAsync_PathTraversal_ThrowsUnauthorizedAccess()
    {
        var attempt = new BlobRequest("../escape.txt") { Type = BlobTypes.File };

        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await _service.DeleteAsync(attempt));
    }

    [Fact]
    public async Task CheckExistsAsync_LeadingSlash_TreatedAsRelative()
    {
        // Arrange: write a file directly under root, then look it up with a leading slash.
        await File.WriteAllTextAsync(Path.Combine(_root, "lead.txt"), "x");

        var blob = new BlobRequest("/lead.txt") { Type = BlobTypes.File };

        // Act
        var exists = await _service.CheckExistsAsync(blob);

        // Assert: leading slash must be stripped, resolving to <root>/lead.txt
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_FileWithCancelledToken_ReturnsFalseAndKeepsFile()
    {
        var path = Path.Combine(_root, "keep.txt");
        await File.WriteAllTextAsync(path, "x");

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await _service.DeleteAsync(
            new BlobRequest("keep.txt") { Type = BlobTypes.File },
            cts.Token);

        result.ShouldBeFalse();
        File.Exists(path).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_FolderWithCancelledToken_ReturnsFalseAndKeepsFolder()
    {
        var folder = Path.Combine(_root, "keep-folder");
        Directory.CreateDirectory(folder);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await _service.DeleteAsync(
            new BlobRequest("keep-folder") { Type = BlobTypes.Directory },
            cts.Token);

        result.ShouldBeFalse();
        Directory.Exists(folder).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_NestedPath_CreatesMissingDirectories()
    {
        var fileName = "nested/deep/created.txt";
        var blob = new BlobDetails.BlobData(fileName, new BinaryData("hi"u8.ToArray()))
        {
            Overwrite = false,
            Type = BlobTypes.File
        };

        var saved = await _service.SaveAsync(blob);

        saved.ShouldBe(fileName);
        File.Exists(Path.Combine(_root, "nested", "deep", "created.txt")).ShouldBeTrue();
    }

    #endregion
}
