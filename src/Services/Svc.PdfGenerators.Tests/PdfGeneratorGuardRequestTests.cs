using System.Collections.Concurrent;
using System.Reflection;
using DKNet.Svc.PdfGenerators;
using PuppeteerSharp;
using Shouldly;

namespace Svc.PdfGenerators.Tests;

/// <summary>
///     DRK-1902 rules R3 and R4 on the request-interception handler, driven with a fake <see cref="IRequest" />
///     so the outcome does not depend on what Chromium or DNS does with the request.
/// </summary>
public class PdfGeneratorGuardRequestTests
{
    #region Methods

    [Theory]
    [InlineData("http://8.8.8.8/img")]
    [InlineData("http://127.0.0.1/img")]
    public async Task GuardRequestAsync_ContinueOrAbortThrowsTimeout_DoesNotThrow(string url)
    {
        // Arrange
        var request = FakeRequest.Create(url, Task.FromException(new TimeoutException("CDP protocol timeout")));
        await using var generator = new PdfGenerator();

        // Act
        var guard = () => generator.GuardRequestAsync(request);

        // Assert
        await guard.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task GuardRequestAsync_DecisionThrows_AbortsAndDoesNotContinue()
    {
        // Arrange: a 308-character host name is longer than Dns.GetHostAddressesAsync accepts, so the decision throws.
        var host = string.Join('.', Enumerable.Repeat(new string('a', 60), 5)) + ".com";
        var request = FakeRequest.Create($"http://{host}/img", Task.CompletedTask);
        await using var generator = new PdfGenerator();

        // Act
        await generator.GuardRequestAsync(request);

        // Assert
        FakeRequest.CallsOf(request).ShouldBe([nameof(IRequest.AbortAsync)]);
    }

    #endregion

    /// <summary>
    ///     <see cref="IRequest" /> fake: returns a fixed URL, records every <c>ContinueAsync</c>/<c>AbortAsync</c>
    ///     call and answers both with the same task.
    /// </summary>
    public class FakeRequest : DispatchProxy
    {
        #region Fields

        private readonly ConcurrentQueue<string> _calls = new();
        private Task _outcome = Task.CompletedTask;
        private string _url = string.Empty;

        #endregion

        #region Methods

        public static IRequest Create(string url, Task outcome)
        {
            var request = Create<IRequest, FakeRequest>();
            var fake = (FakeRequest)(object)request;
            fake._url = url;
            fake._outcome = outcome;
            return request;
        }

        public static string[] CallsOf(IRequest request) => [.. ((FakeRequest)(object)request)._calls];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_Url":
                    return _url;
                case nameof(IRequest.ContinueAsync):
                case nameof(IRequest.AbortAsync):
                    _calls.Enqueue(targetMethod.Name);
                    return _outcome;
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }

        #endregion
    }
}
