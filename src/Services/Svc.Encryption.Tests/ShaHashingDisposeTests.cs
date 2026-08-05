using System;
using DKNet.Svc.Encryption;
using Shouldly;
using Xunit;

namespace Svc.Encryption.Tests;

public class ShaHashingDisposeTests
{
    #region Methods

    [Fact]
    public void ShaHashing_AfterDispose_ThrowsObjectDisposedException_OnComputeSha256()
    {
        var sha = new ShaHashing();
        sha.Dispose();
        Should.Throw<ObjectDisposedException>(() => sha.ComputeSha256("input"));
    }

    [Fact]
    public void ShaHashing_AfterDispose_ThrowsObjectDisposedException_OnComputeSha512()
    {
        var sha = new ShaHashing();
        sha.Dispose();
        Should.Throw<ObjectDisposedException>(() => sha.ComputeSha512("input"));
    }

    [Fact]
    public void ShaHashing_AfterDispose_ThrowsObjectDisposedException_OnVerifySha256()
    {
        var sha = new ShaHashing();
        sha.Dispose();
        Should.Throw<ObjectDisposedException>(() => sha.VerifySha256("input", "expected"));
    }

    [Fact]
    public void ShaHashing_AfterDispose_ThrowsObjectDisposedException_OnVerifySha512()
    {
        var sha = new ShaHashing();
        sha.Dispose();
        Should.Throw<ObjectDisposedException>(() => sha.VerifySha512("input", "expected"));
    }

    #endregion
}
