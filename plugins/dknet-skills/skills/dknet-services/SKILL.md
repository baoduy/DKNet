---
name: dknet-services
description: Covers DKNet.Svc.Encryption, DKNet.Svc.PdfGenerators, and DKNet.Svc.Transformation — three independent, explicitly-invoked .NET services. Encryption: AES-GCM encrypt string .NET, RSA sign verify, HMAC SHA hash helper, Base64/Base64URL, via AddAesGcmEncryption, AddRsaEncryption, AddEncryptionServices, IAesGcmEncryption, IRsaEncryption, IHmacHashing, IShaHashing. PdfGenerators: HTML to PDF, Markdown to PDF via PuppeteerSharp and Markdig, via AddPdfGenerator, IPdfGenerator, PdfGeneratorOptions. Transformation: template token replacement like [Name]/{Email}, via AddTransformerService, ITransformerService, TransformOptions. Use when wiring any of these DI calls, picking a cipher/hash helper, converting a document to PDF, or filling a string template without a full engine.
license: MIT
metadata:
  author: baoduy
  version: "0.1.0"
  packages: "DKNet.Svc.Encryption, DKNet.Svc.PdfGenerators, DKNet.Svc.Transformation"
---

# DKNet application services: encryption, PDF, templates

Three independent, explicitly-invoked services with no `ProjectReference` between them and no EF Core or SlimBus
coupling: `DKNet.Svc.Encryption` (AES-GCM, RSA, HMAC/SHA, Base64), `DKNet.Svc.PdfGenerators` (HTML/Markdown → PDF
via PuppeteerSharp + Markdig), and `DKNet.Svc.Transformation` (bracketed-token template filling). Each is safe to
add standalone. Open `references/<PackageId>.md` for the full API surface, options, diagnostics, and testing
notes behind any package named below.

## Packages

| Package | Install | What it gives you | Depends on | Reference |
|---|---|---|---|---|
| `DKNet.Svc.Encryption` | `dotnet add package DKNet.Svc.Encryption` | AES-256-GCM encrypt/decrypt, RSA encrypt/decrypt + sign/verify, HMAC-SHA256/512 + SHA-256/512 hashing, Base64/Base64URL helpers | none (DKNet) | [references/DKNet.Svc.Encryption.md](references/DKNet.Svc.Encryption.md) |
| `DKNet.Svc.PdfGenerators` | `dotnet add package DKNet.Svc.PdfGenerators` | HTML string/file or Markdown file(s) → PDF, headless Chromium print pipeline | none (DKNet) | [references/DKNet.Svc.PdfGenerators.md](references/DKNet.Svc.PdfGenerators.md) |
| `DKNet.Svc.Transformation` | `dotnet add package DKNet.Svc.Transformation` | Bracketed-token template filling (`[Name]`, `{Email}`, `<Amount>`, `{{Ref}}`, or a custom pair) | none (DKNet) | [references/DKNet.Svc.Transformation.md](references/DKNet.Svc.Transformation.md) |

## Quick start

Register all three, then fill a template, render it to PDF, and encrypt the result path — one wiring block, one
pipeline:

```csharp
using DKNet.Svc.Encryption;
using DKNet.Svc.Encryption.Ciphers;
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.Transformation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddTransformerService();                                  // ITransformerService
services.AddPdfGenerator();                                         // IPdfGenerator singleton
services.AddAesGcmEncryption("BASE64_256_BIT_KEY_FROM_CONFIG==");    // IAesGcmEncryption singleton

var provider = services.BuildServiceProvider();
var transformer = provider.GetRequiredService<ITransformerService>();
var pdfGenerator = provider.GetRequiredService<IPdfGenerator>();
var aesGcm = provider.GetRequiredService<IAesGcmEncryption>();

var invoice = new { CustomerName = "Ada Lovelace", Total = 128.50m };
var html = transformer.Transform("<h1>Invoice for [CustomerName]</h1><p>Total: [Total]</p>", invoice);
var pdfPath = await pdfGenerator.ConvertHtmlAsync(html, "invoice.pdf");
var reference = aesGcm.EncryptString(pdfPath);
```

Any one of the three lines in the registration block works alone — nothing here requires the other two packages.

## Rules

1. Call `AddAesGcmEncryption(key)`/`AddRsaEncryption(key)` exactly once, at startup, with the real key sourced
   from configuration or a key vault. A second call with a different key is silently ignored — both guard with
   `services.Any(s => s.ServiceType == typeof(...))`, so the first call's key wins for the process lifetime.
2. Never call `AddEncryptionServices()` expecting a cipher. It registers only `IShaHashing`/`IHmacHashing`. Call
   `AddAesGcmEncryption`/`AddRsaEncryption` explicitly to get `IAesGcmEncryption`/`IRsaEncryption`.
3. Import `DKNet.Svc.Encryption.Ciphers` for `IAesGcmEncryption`/`AesGcmEncryption`/`IRsaEncryption`/
   `RsaEncryption`, and `DKNet.Svc.Encryption.Hashing` for `IHmacHashing`/`HmacHashing`/`IShaHashing`/
   `ShaHashing`. Only `EncryptionSetup` and `Base64StringExtensions` live at the package-root namespace.
4. `using` a hand-built `AesGcmEncryption`/`RsaEncryption`. DI disposes a resolved singleton for you; a
   directly-constructed instance needs an explicit `using` or it leaks the native handle.
5. `AddPdfGenerator(options)` freezes those options into a `TryAddSingleton` at the **first** call. When two
   call sites need different layouts, construct `new PdfGenerator(options)` directly instead of resolving
   `IPdfGenerator`.
6. Hold one long-lived `PdfGenerator` — the DI singleton, or one instance you keep across calls. Each
   `new PdfGenerator()` launches its own Chromium browser; constructing one per request is one Chromium process
   per request.
7. Only `Format`, `IsLandscape`, `MarginOptions`, `Scale`, `HeaderHtml`, `FooterHtml`, and `ChromePath` on
   `PdfGeneratorOptions` change the rendered PDF. `Theme`, `CodeHighlightTheme`, `ModuleOptions`,
   `TableOfContents`, `DocumentTitle`, `MetadataTitle`, `CustomHeadContent`, `KeepHtml`, and
   `EnableAutoLanguageDetection` are settable, serializable, and **inert** — the pipeline never reads them.
8. Alias `PuppeteerSharp.Media.PaperFormat` (or `DKNet.Svc.PdfGenerators.Options.MarginOptions`) when a file
   imports both `PuppeteerSharp.Media` and `DKNet.Svc.PdfGenerators.Options` — the bare name `MarginOptions` is
   ambiguous (`CS0104`) between the two packages' own types.
9. Expect `PuppeteerSharp.ProcessException` (Chromium fails to *launch*, not download) on a non-Apple ARM64 host.
   Verify real PDF-rendering changes on x64 or Apple Silicon, or set `ChromePath` to an ARM-native Chrome install.
10. `AddTransformerService(optionFactory)` is idempotent per app, not per call — the first call's `optionFactory`
    wins. Configure every bracket style, formatter, and `TokenNotFoundBehavior` in that one call.
11. Pass exactly `IDictionary<string, string>` to `Transform`/`TransformAsync` when the data source is a
    dictionary. Any other `IDictionary`, including `Dictionary<string, object>`, throws `ArgumentException`.
12. Don't write dotted token paths. `DKNet.Svc.Transformation` resolves one flat, single-level property per
    token — `[Customer.Name]` is looked up as a literal property named `Customer.Name`, not a nested path.
13. Reach for `DKNet.EfCore.Encryption` (see `dknet-efcore-data-security`), not `DKNet.Svc.Encryption`, when a
    database column should be encrypted transparently on save and decrypted on load.

## How to ...

### Encrypt and decrypt a secret with a DI-registered AES-GCM key

**When**: a service needs to protect a value (a card number, a token) using one durable key resolved from DI.

```csharp
using DKNet.Svc.Encryption;
using DKNet.Svc.Encryption.Ciphers;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAesGcmEncryption("BASE64_256_BIT_KEY_FROM_CONFIG==");
var provider = services.BuildServiceProvider();

var aesGcm = provider.GetRequiredService<IAesGcmEncryption>();
var sealedValue = aesGcm.EncryptString("4111-1111-1111-1111");
var revealed = aesGcm.DecryptString(sealedValue);
```

**Notes**: `EncryptString` generates a fresh random nonce every call, so encrypting the same plaintext twice
never produces the same ciphertext. `DecryptString` throws `CryptographicException` if the ciphertext, key, or
`associatedData` don't match what was used to encrypt.

### Sign data on one service, verify it on another, without sharing the private key

**When**: an order-confirmation service signs a payload; a separate verifier only ever sees the public key.

```csharp
using DKNet.Svc.Encryption.Ciphers;

using var signer = new RsaEncryption();                 // fresh 2048-bit pair
var publicKeyToShare = signer.PublicKey;                 // Base64 PKCS#1 DER
var signature = signer.Sign("order-42:confirmed");

using var verifier = RsaEncryption.FromPublicKey(publicKeyToShare);  // public-only instance
var isAuthentic = verifier.Verify("order-42:confirmed", signature);
```

**Notes**: a `FromPublicKey` instance throws `InvalidOperationException` from `Decrypt`/`Sign` — it holds no
private key. Persist `signer.PrivateKey` (not shown) if the signer needs to survive the process.

### Verify an inbound webhook's HMAC signature

**When**: an HTTP handler must confirm a request body was signed with a shared secret before trusting it.

```csharp
using DKNet.Svc.Encryption.Hashing;

var verifier = new WebhookVerifier(new HmacHashing());
var trusted = verifier.IsTrusted(rawBody: "{...}", secret: "whsec_abc", signatureHeader: "BASE64_SIGNATURE");
```

`WebhookVerifier` is a thin wrapper around `IHmacHashing`:

```csharp
using DKNet.Svc.Encryption.Hashing;

public sealed class WebhookVerifier(IHmacHashing hmac)
{
    public bool IsTrusted(string rawBody, string secret, string signatureHeader) =>
        hmac.VerifySha256(rawBody, secret, signatureHeader);
}
```

**Notes**: `VerifySha256` returns `false` on a mismatch or malformed signature — it never throws for bad input,
only for a blank `message`/`secretKey`/`expectedSignature` (`ArgumentException`).

### Convert Markdown to PDF with a custom page layout

**When**: a report needs a specific paper size, orientation, margins, and a running header/footer.

```csharp
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Options;

var options = new PdfGeneratorOptions
{
    IsLandscape = true,
    MarginOptions = new MarginOptions { Top = "2cm", Bottom = "2cm", Left = "1.5cm", Right = "1.5cm" },
    HeaderHtml = "<div style='font-size:10px;text-align:center;width:100%'>Monthly report</div>",
    FooterHtml = "<div style='font-size:10px;text-align:center;width:100%'>" +
                 "<span class='pageNumber'></span> / <span class='totalPages'></span></div>"
};

var generator = new PdfGenerator(options);
var pdfPath = await generator.ConvertMarkdownFileAsync("report.md", "report.pdf");
```

**Notes**: `HeaderHtml`/`FooterHtml` being non-null is what turns Chromium's header/footer on — there is no
separate boolean switch. `Scale`, `Theme`, and the other layout-only options from **Rules** #7 do not apply here.

### Fill a bracketed-token template from an object or a dictionary

**When**: an email or report body has placeholders and the data is either a DTO or already keyed strings.

```csharp
using DKNet.Svc.Transformation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddTransformerService();
var transformer = services.BuildServiceProvider().GetRequiredService<ITransformerService>();

var fromObject = transformer.Transform("Hello [Name]!", new { Name = "Ada" });
var fromDictionary = transformer.Transform("Hello [Name]!", new Dictionary<string, string> { { "Name", "Ada" } });
```

**Notes**: property/key lookup is case-insensitive. A dictionary parameter must be exactly
`IDictionary<string, string>` (Rule #11). A token with no match throws `UnResolvedTokenException` unless you set
`TokenNotFoundBehavior` to `LeaveAsIs` or `Remove`.

### Load PDF layout options from a Markdown file's own YAML front matter

**When**: the document itself should carry its print layout instead of hardcoded C#.

```csharp
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.PdfGenerators.Services;

var pdfOptions = await InlineOptionsParser.ParseYamlFrontMatter("report.md");
var generator = new PdfGenerator(pdfOptions);
var pdfPath = await generator.ConvertMarkdownFileAsync("report.md", "report.pdf");
```

**Notes**: `ParseYamlFrontMatter` throws `InvalidDataException` if the file's leading `---`/`<!--` block has no
matching closing delimiter. It reads the front matter itself — `PdfGenerator` still renders the Markdown body
underneath it.

### Render a filled template straight to PDF

**When**: combining both packages — fill a template, then convert the result, in one pipeline.

```csharp
using DKNet.Svc.PdfGenerators;
using DKNet.Svc.Transformation;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddTransformerService();
services.AddPdfGenerator();
var provider = services.BuildServiceProvider();

var transformer = provider.GetRequiredService<ITransformerService>();
var pdfGenerator = provider.GetRequiredService<IPdfGenerator>();

var filledHtml = transformer.Transform(
    "<h1>Invoice [InvoiceNumber]</h1><p>Billed to: [CustomerName]</p>",
    new { InvoiceNumber = "INV-042", CustomerName = "Ada Lovelace" });

var pdfPath = await pdfGenerator.ConvertHtmlAsync(filledHtml, "invoice.pdf");
```

**Notes**: `DKNet.Svc.PdfGenerators` has no templating of its own — `DKNet.Svc.Transformation` is the seam that
fills the placeholders before the HTML/Markdown reaches `Convert*`.

## Runtime behaviour

**Encryption**: every call is synchronous, in-process, string-in/string-out. `AesGcmEncryption` generates a
fresh random nonce per `EncryptString` call and serializes encrypt/decrypt through one lock per instance. Nothing
is cached, persisted, or rotated by the package — the caller owns key lifecycle end to end.

**PdfGenerators**: on the first `Convert*` call, the instance ensures Chromium is available (skips the download
if `ChromePath` is set, otherwise downloads it behind a process-wide lock) and launches one headless browser,
which it keeps open and reuses for every later call on that same instance. Markdown is rendered to HTML first
(via Markdig), then the HTML is loaded into a page with no base URL and printed to PDF with `PrintBackground`
forced on. Disposing the instance (or the DI container shutting down) closes that browser.

**Transformation**: `TransformerService` builds its token extractors once, at construction, from
`TransformOptions.DefaultDefinitions` at that moment. Each `Transform`/`TransformAsync` call then: extracts every
token, resolves each one (call parameters first, then `GlobalParameters`, with a per-call cache that only
dedupes repeats within that one call), applies `TokenNotFoundBehavior` to any unresolved token, formats the
result through `Formatter`, and rebuilds the string in one pass.

## Gotchas

- **`AddPdfGenerator`/`AddAesGcmEncryption`/`AddRsaEncryption`/`AddTransformerService` are all idempotent per
  process, not per call.** A later call with different options/keys is silently dropped everywhere in this
  skill's three packages — register each exactly once, with the configuration you actually want, and don't
  expect a second call anywhere in the composition root to change anything.
- **`PdfGenerator` never fires `IConversionEvents`.** `HtmlConverting`/`TemplateModelCreating`/`TempPdfCreated`
  exist on the interface but nothing in the shipped pipeline raises them — don't build a feature that depends on
  intercepting Markdown-to-HTML or template-model creation through it.
- **A fresh `PdfGenerator` instance means a fresh Chromium browser.** There is no cross-instance pool; a service
  that does `new PdfGenerator()` per request launches a browser per request. Keep one instance (or the DI
  singleton) around.
- **Relative asset URLs never resolve inside PDF HTML.** Content is set directly with no base URL — use absolute
  URLs or inline (base64) images/CSS.
- **Transformation's per-call cache cannot leak between calls.** It is a fresh dictionary created inside each
  `Transform`/`TransformAsync` call, not a field on the instance — reusing one `ITransformerService` across many
  different records/requests is safe, including as a singleton if you choose to register it that way.
- **`ignoreCase` on `IShaHashing.Verify*` has no effect.** Comparison runs on already-decoded, case-normalized
  bytes; the flag is read but never changes the result.

## Do not

- Do not call `IPasswordAesEncryption`/`PasswordAesEncryption`, `IAesEncryption`/`AesEncryption`, or
  `EncryptionSetup.AddAesEncryption` — none of these exist. The only symmetric cipher this skill's Encryption
  package ships is AES-**GCM** via `IAesGcmEncryption`/`AddAesGcmEncryption`.
- Do not call `PdfGenerator.GenerateFromTemplateAsync` or expect a Razor/model-binding overload on
  `IPdfGenerator` — there is no templating API in `DKNet.Svc.PdfGenerators`. Render the string yourself (or use
  `DKNet.Svc.Transformation`) before calling `Convert*`.
- Do not reference `ICustomConverter`, `TransformationException`, a `DisabledLocalCache` option, or template
  syntax like `{amount:currency:USD}` on `DKNet.Svc.Transformation` — none of these exist anywhere in the
  package.
- Do not write `[Customer.Name]` and expect nested-property resolution, or import
  `DKNet.Svc.Encryption.IAesGcmEncryption`/`DKNet.Svc.Encryption.IRsaEncryption` (wrong namespace — they live
  under `.Ciphers`):

```csharp
// no-compile
using DKNet.Svc.Encryption; // IAesGcmEncryption is NOT here — it's DKNet.Svc.Encryption.Ciphers

var services = new ServiceCollection();
services.AddAesGcmEncryption(""); // empty key throws ArgumentException, not a silent random key
var pwEnc = new PasswordAesEncryption("hunter2"); // never shipped
var doc = pdfGenerator.GenerateFromTemplateAsync("template.html", model); // no such API
var value = transformer.Transform("[Customer.Name]", customer); // dotted paths are not resolved
```

- Do not construct `TokenExtractor`/`TokenResolver` from `DKNet.Svc.Transformation.TokenExtractors` — both are
  `internal sealed`; they will not compile outside the package.
- Do not add manual `byte[]` plumbing around the Encryption ciphers/hashers — every public method is already
  `string`-in/`string`-out.

## Related skills

- `dknet-packages` — DKNet package router and setup order; start there if you haven't picked a package yet.
- `dknet-efcore-data-security` — transparent EF Core column encryption (`DKNet.EfCore.Encryption`); use it
  instead of this skill's `DKNet.Svc.Encryption` for a database column, not a call-site value.
- `dknet-blob-storage` — blob storage adapters; pairs with `DKNet.Svc.Encryption` (encrypt before `SaveAsync`)
  and `DKNet.Svc.PdfGenerators` (store the converted PDF) when the result needs to leave local disk.
- `dknet-aspcore-api` — minimal-API endpoints and start-up tasks; run a slow PDF conversion as a background task
  from there instead of inline in a request handler.
- `dknet-efcore-domain-model`, `dknet-efcore-specifications`, `dknet-efcore-save-pipeline`, `dknet-codegen`,
  `dknet-slimbus-cqrs`, `dknet-idempotency` — none of these compose directly with the three packages in this
  skill; reach for them for the EF Core domain model, querying, the save pipeline, source generators, CQRS, and
  idempotent endpoints respectively.
- `dknet-core-utilities` — foundation helpers (`DKNet.Fw.Extensions`, `DKNet.RandomCreator`); has no
  relationship to this skill's packages beyond both being framework-wide utility libraries.
- `dknet-testing` — testing patterns for code built on DKNet, and for working inside the DKNet repo itself.

## References

- [references/DKNet.Svc.Encryption.md](references/DKNet.Svc.Encryption.md) — full API surface, options,
  runtime behaviour, diagnostics, gotchas, and testing notes for AES-GCM, RSA, HMAC/SHA, and Base64 helpers.
  Docs: https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.Encryption.md
- [references/DKNet.Svc.PdfGenerators.md](references/DKNet.Svc.PdfGenerators.md) — full API surface, the
  "Applied by `PdfGenerator`?" options table, runtime behaviour, diagnostics, gotchas, and testing notes for
  HTML/Markdown → PDF conversion.
  Docs: https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.PdfGenerators.md
- [references/DKNet.Svc.Transformation.md](references/DKNet.Svc.Transformation.md) — full API surface, options,
  runtime behaviour, diagnostics, gotchas, and testing notes for bracketed-token template filling.
  Docs: https://github.com/baoduy/DKNet/blob/dev/docs/Services/DKNet.Svc.Transformation.md
- NuGet: https://www.nuget.org/packages/DKNet.Svc.Encryption, https://www.nuget.org/packages/DKNet.Svc.PdfGenerators,
  https://www.nuget.org/packages/DKNet.Svc.Transformation
- Rendered docs site: https://baoduy.github.io/DKNet/
