using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Shouldly;

namespace Svc.PdfGenerators.Tests;

/// <summary>
///     Acceptance tests for DRK-1902: rendering caller-supplied HTML must not make the server request
///     loopback / private hosts, and must not run page JavaScript, unless the consumer opts in.
///     A local HTTP listener on loopback records every request Chromium sends while rendering.
///     Each test awaits the PDF before it asserts on the listener, so a zero is never a timing artefact.
/// </summary>
[Collection("PdfGeneratorChrome")]
public class PdfGeneratorSsrfGuardTests
{
    #region Methods

    [Fact]
    public async Task ConvertHtmlAsync_DefaultOptions_SendsNoRequestToLoopbackHosts()
    {
        // Arrange
        await using var listener = ProbeListener.Start();
        var html = $"""
                    <html><head>
                    <link rel="stylesheet" href="{listener.IpUrl}/css">
                    <link rel="stylesheet" href="{listener.LocalhostUrl}/css-localhost">
                    </head><body>
                    <h1>probe</h1>
                    <img src="{listener.IpUrl}/img">
                    <img src="{listener.LocalhostUrl}/img-localhost">
                    <iframe src="{listener.IpUrl}/iframe"></iframe>
                    <iframe src="{listener.LocalhostUrl}/iframe-localhost"></iframe>
                    </body></html>
                    """;
        await using var generator = new PdfGenerator();
        var pdfPath = NewPdfPath();

        try
        {
            // Act
            var result = await generator.ConvertHtmlAsync(html, pdfPath);

            // Assert
            File.Exists(result).ShouldBeTrue();
            listener.Paths.ShouldBeEmpty();
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public async Task ConvertHtmlAsync_AllowPrivateNetworkRequests_SendsImageRequestsToLoopbackHosts()
    {
        // Arrange
        await using var listener = ProbeListener.Start();
        var html = $"""
                    <h1>probe</h1>
                    <img src="{listener.IpUrl}/img">
                    <img src="{listener.LocalhostUrl}/img-localhost">
                    """;
        await using var generator = new PdfGenerator(new PdfGeneratorOptions { AllowPrivateNetworkRequests = true });
        var pdfPath = NewPdfPath();

        try
        {
            // Act
            var result = await generator.ConvertHtmlAsync(html, pdfPath);

            // Assert
            File.Exists(result).ShouldBeTrue();
            listener.Paths.ShouldContain("/img");
            listener.Paths.ShouldContain("/img-localhost");
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public async Task ConvertHtmlAsync_AllowPrivateNetworkRequestsWithDefaultJavaScript_DoesNotRunPageScripts()
    {
        // Arrange
        await using var listener = ProbeListener.Start();
        var html = ScriptProbeHtml(listener);
        await using var generator = new PdfGenerator(new PdfGeneratorOptions { AllowPrivateNetworkRequests = true });
        var pdfPath = NewPdfPath();

        try
        {
            // Act
            var result = await generator.ConvertHtmlAsync(html, pdfPath);

            // Assert
            File.Exists(result).ShouldBeTrue();
            listener.Paths.ShouldContain("/img");
            listener.Paths.ShouldNotContain("/js");
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public async Task ConvertHtmlAsync_AllowPrivateNetworkRequestsWithJavaScriptEnabled_RunsPageScripts()
    {
        // Arrange
        await using var listener = ProbeListener.Start();
        var html = ScriptProbeHtml(listener);
        await using var generator = new PdfGenerator(new PdfGeneratorOptions
        {
            AllowPrivateNetworkRequests = true,
            EnableJavaScript = true
        });
        var pdfPath = NewPdfPath();

        try
        {
            // Act
            var result = await generator.ConvertHtmlAsync(html, pdfPath);

            // Assert
            File.Exists(result).ShouldBeTrue();
            listener.Paths.ShouldContain("/img");
            listener.Paths.ShouldContain("/js");
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public void PdfGeneratorOptions_NewInstance_DisablesPrivateNetworkRequestsAndJavaScript()
    {
        // Act
        var options = new PdfGeneratorOptions();

        // Assert
        options.AllowPrivateNetworkRequests.ShouldBeFalse();
        options.EnableJavaScript.ShouldBeFalse();
    }

    private static string NewPdfPath() => Path.Combine(Path.GetTempPath(), $"ssrf-probe-{Guid.NewGuid():N}.pdf");

    /// <summary>
    ///     The <c>/img</c> request proves the listener is reachable in the same render; the <c>/js</c> request is
    ///     a synchronous XHR, so when scripts run it reaches the listener before the load event and the PDF.
    /// </summary>
    private static string ScriptProbeHtml(ProbeListener listener) =>
        $$"""
          <h1>probe</h1>
          <img src="{{listener.IpUrl}}/img">
          <script>
          var xhr = new XMLHttpRequest();
          xhr.open('GET', '{{listener.IpUrl}}/js', false);
          try { xhr.send(); } catch (e) { }
          </script>
          """;

    #endregion

    /// <summary>
    ///     Loopback HTTP listener answering every request with <c>200 OK</c> and recording its path.
    ///     Registers both <c>127.0.0.1</c> and <c>localhost</c> prefixes so neither host name is rejected
    ///     before it is recorded.
    /// </summary>
    private sealed class ProbeListener : IAsyncDisposable
    {
        #region Fields

        private readonly HttpListener _listener = new();
        private readonly Task _loop;
        private readonly ConcurrentQueue<string> _paths = new();

        #endregion

        #region Constructors

        private ProbeListener(int port)
        {
            IpUrl = $"http://127.0.0.1:{port}";
            LocalhostUrl = $"http://localhost:{port}";
            _listener.Prefixes.Add($"{IpUrl}/");
            _listener.Prefixes.Add($"{LocalhostUrl}/");
            _listener.Start();
            _loop = Task.Run(AcceptLoopAsync);
        }

        #endregion

        #region Properties

        public string IpUrl { get; }

        public string LocalhostUrl { get; }

        public IReadOnlyCollection<string> Paths => _paths.ToArray();

        #endregion

        #region Methods

        public static ProbeListener Start()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return new ProbeListener(port);
        }

        private async Task AcceptLoopAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception) when (!_listener.IsListening)
                {
                    return;
                }

                _paths.Enqueue(context.Request.Url!.AbsolutePath);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                var body = "ok"u8.ToArray();
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try
            {
                await _loop;
            }
            catch (Exception)
            {
                // listener shut down while a request was in flight
            }
        }

        #endregion
    }
}
