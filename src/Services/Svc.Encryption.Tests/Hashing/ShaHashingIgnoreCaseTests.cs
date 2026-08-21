using System;
using System.Linq;
using DKNet.Svc.Encryption.Hashing;
using Shouldly;
using Xunit;

namespace Svc.Encryption.Tests.Hashing;

public class ShaHashingIgnoreCaseTests
{
    #region Methods

    [Fact]
    public void ShaHashing_VerifySha256_IgnoreCaseTrueOrFalse_ReturnsSameResult()
    {
        using var sha = new ShaHashing();
        var message = "test message";
        var hex = sha.ComputeSha256(message);

        var mixedCaseHex = new string([.. hex.Select((c, i) => i % 2 == 0 ? char.ToUpper(c) : char.ToLower(c))]);

        var rTrue = sha.VerifySha256(message, mixedCaseHex, true);
        var rFalse = sha.VerifySha256(message, mixedCaseHex, false);

        rTrue.ShouldBe(rFalse);
        rTrue.ShouldBeTrue();
    }

    [Fact]
    public void ShaHashing_VerifySha512_IgnoreCaseTrueOrFalse_ReturnsSameResult()
    {
        using var sha = new ShaHashing();
        var message = "test message";
        var hex = sha.ComputeSha512(message);

        var mixedCaseHex = new string([.. hex.Select((c, i) => i % 2 == 0 ? char.ToUpper(c) : char.ToLower(c))]);

        var rTrue = sha.VerifySha512(message, mixedCaseHex, true);
        var rFalse = sha.VerifySha512(message, mixedCaseHex, false);

        rTrue.ShouldBe(rFalse);
        rTrue.ShouldBeTrue();
    }

    #endregion
}
