using System;
using DKNet.Svc.Encryption;
using Shouldly;
using Xunit;

namespace Svc.Encryption.Tests;

public class HmacHashingDisposeTests
{
    #region Methods

    [Fact]
    public void HmacHashing_AfterDispose_ThrowsObjectDisposedException_OnComputeSha256()
    {
        var hmac = new HmacHashing();
        hmac.Dispose();
        Should.Throw<ObjectDisposedException>(() => hmac.ComputeSha256("message", "key"));
    }

    [Fact]
    public void HmacHashing_AfterDispose_ThrowsObjectDisposedException_OnComputeSha512()
    {
        var hmac = new HmacHashing();
        hmac.Dispose();
        Should.Throw<ObjectDisposedException>(() => hmac.ComputeSha512("message", "key"));
    }

    [Fact]
    public void HmacHashing_AfterDispose_ThrowsObjectDisposedException_OnVerifySha256()
    {
        var hmac = new HmacHashing();
        hmac.Dispose();
        Should.Throw<ObjectDisposedException>(() => hmac.VerifySha256("message", "key", "sig"));
    }

    [Fact]
    public void HmacHashing_AfterDispose_ThrowsObjectDisposedException_OnVerifySha512()
    {
        var hmac = new HmacHashing();
        hmac.Dispose();
        Should.Throw<ObjectDisposedException>(() => hmac.VerifySha512("message", "key", "sig"));
    }

    #endregion
}
