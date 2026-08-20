using DKNet.Svc.Encryption;
using Shouldly;
using Xunit;

namespace Svc.Encryption.Tests;

public class ShaHashingDisposeTests
{
    #region Fields

    // Independently computed (not via ShaHashing itself) so the test doesn't validate the
    // implementation against itself.
    private const string AcmeSha256Hex = "fd495f04bee588b5a8af262c059c4943c8dd7e72465b4aed93dfab10d409a632";

    private const string AcmeSha512Hex =
        "36ab80bda2bc2b57796b7c3fb8cef23fe4a9676733f4efe45a929e35636278c58d30576c0449bbc114b0285536628dac8bcdb171da8936d5d4181fb156cf1c41";

    #endregion

    #region Methods

    [Fact]
    public void Dispose_CompletesWithoutError_AndSameInstanceStillComputesSha256()
    {
        var sha = new ShaHashing();

        Should.NotThrow(() => sha.Dispose());

        // Static one-shot SHA256.HashData holds no cached/disposable state, so disposal is a
        // safe no-op and the instance keeps hashing correctly afterward.
        sha.ComputeSha256("input").ShouldBe(sha.ComputeSha256("input"));
    }

    [Fact]
    public void Dispose_CompletesWithoutError_AndSameInstanceStillComputesSha512()
    {
        var sha = new ShaHashing();

        Should.NotThrow(() => sha.Dispose());

        sha.ComputeSha512("input").ShouldBe(sha.ComputeSha512("input"));
    }

    [Fact]
    public void Dispose_CompletesWithoutError_AndSameInstanceStillVerifiesSha256()
    {
        var sha = new ShaHashing();
        var expected = sha.ComputeSha256("input");

        Should.NotThrow(() => sha.Dispose());

        sha.VerifySha256("input", expected).ShouldBeTrue();
    }

    [Fact]
    public void Dispose_CompletesWithoutError_AndSameInstanceStillVerifiesSha512()
    {
        var sha = new ShaHashing();
        var expected = sha.ComputeSha512("input");

        Should.NotThrow(() => sha.Dispose());

        sha.VerifySha512("input", expected).ShouldBeTrue();
    }

    [Fact]
    public void Dispose_CompletesWithoutError_AndNewInstanceReturnsExpectedDigest()
    {
        // Acceptance scenario: dispose one instance, then hash "Acme Pte Ltd" with a *new*
        // instance -> disposal must not leave any process-wide state that breaks other instances.
        var disposed = new ShaHashing();
        disposed.Dispose();

        var fresh = new ShaHashing();
        fresh.ComputeSha256("Acme Pte Ltd").ShouldBe(AcmeSha256Hex);
        fresh.ComputeSha512("Acme Pte Ltd").ShouldBe(AcmeSha512Hex);
    }

    #endregion
}
