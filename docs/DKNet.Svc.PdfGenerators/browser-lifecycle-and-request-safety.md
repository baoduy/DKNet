# Browser lifecycle and request safety

Part of [DKNet.Svc.PdfGenerators](index.md). Every `PdfGenerator` instance shares one headless Chromium browser
across every conversion it performs, and guards every request the rendered content tries to make.

```csharp
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddPdfGenerator(new PdfGeneratorOptions
{
    // Off by default: caller-supplied Markdown/HTML cannot run scripts or reach loopback,
    // link-local, private (RFC 1918), CGNAT or unique-local hosts (SSRF). Enable only for
    // content you fully trust.
    EnableJavaScript = false,
    AllowPrivateNetworkRequests = false
});
using var provider = services.BuildServiceProvider();

var generator = provider.GetRequiredService<IPdfGenerator>();
var path = await generator.ConvertHtmlAsync("<img src='https://example.com/logo.png'>");
Console.WriteLine(path);
```

On first use, `PdfGenerator` downloads a Chromium build through PuppeteerSharp's `BrowserFetcher` unless
`ChromePath` points at an existing install; the download is serialized across concurrent first calls so it only
happens once. The browser itself launches once per `PdfGenerator` instance and is reused for every conversion,
relaunching automatically only if the previous instance is closed or disconnected (for example, if it crashed).
When the `CI` or `GITHUB_ACTIONS` environment variable equals `"true"`, Chromium launches with `--no-sandbox
--disable-setuid-sandbox` — the flags a containerized runner normally needs.

Every page sets `SetJavaScriptEnabledAsync(Options.EnableJavaScript)` and turns on request interception; each
outgoing request is checked by `RequestGuard.IsAllowedAsync` before it is allowed to continue. A `data:` URL is
always allowed. Any other URL must use `http` or `https`, and — unless `AllowPrivateNetworkRequests` is `true` —
must not resolve to a loopback, unspecified, link-local, RFC 1918 private, CGNAT, or IPv6 unique-local address. A
request that fails either check is aborted, not just blocked from completing; a guard decision that throws also
aborts the request, so the guard fails closed.

![GetBrowserAsync launches Chromium through BrowserFetcher and Puppeteer.LaunchAsync, serialized by two locks; once a page is open, every outgoing request fires RequestGuard.IsAllowedAsync, which either continues or aborts the request before PdfAsync writes the file.](diagrams/browser-lifecycle-and-request-safety.svg)

The diagram does not show what happens inside Chromium once a request is allowed — PuppeteerSharp and the browser
process own that.

## ⚙️ Configuration reference

| Option | Type | Default | Effect | Applied |
|---|---|---|---|---|
| `EnableJavaScript` | `bool` | `false` | Runs JavaScript in the rendered content. WebSocket/WebRTC connections a script opens are **not** covered by the request guard. | ✅ |
| `AllowPrivateNetworkRequests` | `bool` | `false` | Allows rendered content to reach loopback, unspecified, link-local, private (RFC 1918), CGNAT and unique-local hosts. Requests with a scheme other than `http`, `https` or `data` are blocked regardless. | ✅ |
| `ChromePath` | `string?` | `null` | Path to an existing Chromium/Chrome; when `null`, a build is downloaded on demand. | ✅ |

## 🧱 Where it fits

Both [Convert Markdown to a PDF](convert-markdown-to-pdf.md) and [Convert HTML to a PDF](convert-html-to-pdf.md)
go through this same browser and guard on every call. Generating a PDF is slow and CPU-bound — reach for
`DKNet.AspCore.Tasks` to run it as a background job rather than inside a request when the document is large.

## ⚠️ Gotchas & limits

- **DNS-rebinding is not closed.** The address `RequestGuard` resolves is checked separately from the one Chromium actually connects to, so a host that answers a public address to the guard and a private one to the browser slips through. Closing that window needs routing the browser through a proxy that pins the resolved address.
- **`AllowPrivateNetworkRequests` does not relax the scheme check.** A `file:`, `ftp:`, or other non-`http(s)`/`data:` URL is blocked whatever this option is set to.
- **A browser per `PdfGenerator` instance, not per conversion.** There is no page or browser pool across instances; high-volume conversion from a single singleton still serializes through the `_browserLock` used to launch it, but concurrent pages on the same browser instance run independently.
- **Chromium download is x64.** PuppeteerSharp fetches an x64 Chromium build; on a non-Apple ARM64 host the download succeeds but the launch fails. Apple Silicon runs it fine under Rosetta.
- **Options are frozen at registration.** `AddPdfGenerator` is idempotent — the first call's `EnableJavaScript`/`AllowPrivateNetworkRequests` are the ones the singleton keeps.
