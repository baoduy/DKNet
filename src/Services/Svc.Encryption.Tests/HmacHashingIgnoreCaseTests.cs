using System;
using System.Linq;
using DKNet.Svc.Encryption;
using Shouldly;
using Xunit;

namespace Svc.Encryption.Tests;

public class HmacHashingIgnoreCaseTests
{
    #region Methods

    [Fact]
    public void HmacHashing_VerifySha256_IgnoreCaseTrueOrFalse_ReturnsSameResult()
    {
        using var hmac = new HmacHashing();
        var message = "test message";
        var secretKey = "secret";
        var sig = hmac.ComputeSha256(message, secretKey, false); // hex signature

        var mixedCaseSig = new string([.. sig.Select((c, i) => i % 2 == 0 ? char.ToUpper(c) : char.ToLower(c))]);

        var rTrue = hmac.VerifySha256(message, secretKey, mixedCaseSig, false, true);
        var rFalse = hmac.VerifySha256(message, secretKey, mixedCaseSig, false, false);

        rTrue.ShouldBe(rFalse);
        rTrue.ShouldBeTrue();
    }

    [Fact]
    public void HmacHashing_VerifySha512_IgnoreCaseTrueOrFalse_ReturnsSameResult()
    {
        using var hmac = new HmacHashing();
        var message = "test message";
        var secretKey = "secret";
        var sig = hmac.ComputeSha512(message, secretKey, false); // hex signature

        var mixedCaseSig = new string([.. sig.Select((c, i) => i % 2 == 0 ? char.ToUpper(c) : char.ToLower(c))]);

        var rTrue = hmac.VerifySha512(message, secretKey, mixedCaseSig, false, true);
        var rFalse = hmac.VerifySha512(message, secretKey, mixedCaseSig, false, false);

        rTrue.ShouldBe(rFalse);
        rTrue.ShouldBeTrue();
    }

    #endregion
}
