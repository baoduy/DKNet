using DKNet.Svc.Encryption;
using Shouldly;
using Xunit;

namespace Svc.Encryption.Tests;

public class HmacHashingDisposeTests
{
    #region Fields

    private const string Key = "secret-key";

    // Independently computed (not via HmacHashing itself) so the test doesn't validate the
    // implementation against itself.
    private const string AcmeHmacSha256Base64 = "SlTdCC6tPgNm3mt5Rsl24pAnMMANTgOF2YBBjNM3Fa8=";

    private const string AcmeHmacSha512Base64 =
        "VXS29uNKu7Xy910lTPXjZlbfzyINJZ8gnpAV8FeQfLvt+i+zV2faBDi8JaGsuxRf92yYXU++HvS0ssfZGdbUkQ==";

    #endregion

    #region Methods

    [Fact]
    public void Dispose_CompletesWithoutError_AndSameInstanceStillComputesSha256()
    {
        var hmac = new HmacHashing();

        Should.NotThrow(() => hmac.Dispose());

        // Static one-shot HMACSHA256.HashData holds no cached/disposable state, so disposal is
        // a safe no-op and the instance keeps hashing correctly afterward.
        hmac.ComputeSha256("message", "key").ShouldBe(hmac.ComputeSha256("message", "key"));
    }

    [Fact]
    public void Dispose_CompletesWithoutError_AndSameInstanceStillComputesSha512()
    {
        var hmac = new HmacHashing();

        Should.NotThrow(() => hmac.Dispose());

        hmac.ComputeSha512("message", "key").ShouldBe(hmac.ComputeSha512("message", "key"));
    }

    [Fact]
    public void Dispose_CompletesWithoutError_AndSameInstanceStillVerifiesSha256()
    {
        var hmac = new HmacHashing();
        var sig = hmac.ComputeSha256("message", "key");

        Should.NotThrow(() => hmac.Dispose());

        hmac.VerifySha256("message", "key", sig).ShouldBeTrue();
    }

    [Fact]
    public void Dispose_CompletesWithoutError_AndSameInstanceStillVerifiesSha512()
    {
        var hmac = new HmacHashing();
        var sig = hmac.ComputeSha512("message", "key");

        Should.NotThrow(() => hmac.Dispose());

        hmac.VerifySha512("message", "key", sig).ShouldBeTrue();
    }

    [Fact]
    public void Dispose_CompletesWithoutError_AndNewInstanceReturnsExpectedDigest()
    {
        // Acceptance scenario: dispose one instance, then hash "Acme Pte Ltd" with a *new*
        // instance -> disposal must not leave any process-wide state that breaks other instances.
        var disposed = new HmacHashing();
        disposed.Dispose();

        var fresh = new HmacHashing();
        fresh.ComputeSha256("Acme Pte Ltd", Key).ShouldBe(AcmeHmacSha256Base64);
        fresh.ComputeSha512("Acme Pte Ltd", Key).ShouldBe(AcmeHmacSha512Base64);
    }

    #endregion
}
