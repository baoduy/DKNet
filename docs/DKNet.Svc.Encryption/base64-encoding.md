# Base64 and Base64URL encoding

Part of [DKNet.Svc.Encryption](index.md). `Base64StringExtensions` exposes `ToBase64String`/`FromBase64String` for
the standard alphabet and `ToBase64UrlString`/`FromBase64UrlString` for the URL-safe RFC 4648 variant, plus
`IsBase64String` for validating the standard form — all as `this string` extension methods, static calls work too.

```csharp
using DKNet.Svc.Encryption;

var token = "???>>>subjects".ToBase64UrlString();
var back = token.FromBase64UrlString();
Console.WriteLine(token); // Pz8_Pj4-c3ViamVjdHM
Console.WriteLine(back);  // ???>>>subjects
Console.WriteLine(token.IsBase64String()); // False (- and _ are not in the standard Base64 alphabet)
```

None — no DI registration needed; `Base64StringExtensions`' methods are static extension methods you call directly, as above.

The URL variants are useful for JWT-style payloads without pulling in a JWT library: they replace `+`/`/` with
`-`/`_` and omit `=` padding. Whitespace or empty input returns `string.Empty` from both decode helpers, and
`IsBase64String` returns `false` for it.

![ToBase64UrlString encodes UTF-8 input with the URL-safe alphabet; IsBase64String recognizes a token that stayed within the standard alphabet but returns false for one containing '-' or '_', while FromBase64UrlString decodes either form back to the original string.](diagrams/base64-encoding.svg)

The diagram cannot show the standard-alphabet pair (`ToBase64String`/`FromBase64String`) on its own — that path
never touches `IsBase64String`'s URL-character check at all, because it never produces `-`/`_` in the first place.

## ⚙️ Configuration reference

None — `Base64StringExtensions` reads no options and exposes no override point. It is a static extension class
with no DI registration; call the methods directly on any `string`.

## 🧱 Where it fits

| DKNet consumer | Package | What it does with this feature |
|---|---|---|
| `AesGcmEncryption` | [AES-GCM authenticated encryption](aes-gcm-encryption.md) | Calls `ToBase64String`/`FromBase64String` internally to pack and unpack its `nonce:tag:cipher` package. |

No other DKNet package reaches these extensions. **Entry point in your app:** `using DKNet.Svc.Encryption;`, then
call `ToBase64String`/`FromBase64UrlString`/etc. on any `string` — no DI registration needed.

## ⚠️ Gotchas & limits

- **`IsBase64String` validates the standard alphabet only.** A token produced by `ToBase64UrlString` can return
  `false` from `IsBase64String` even though `FromBase64UrlString` decodes it correctly — there's no
  `IsBase64UrlString` helper to check the URL-safe form directly.
- **No implicit alphabet detection.** Decoding with the wrong helper (`FromBase64String` on a Base64URL token
  containing `-`/`_`) throws `FormatException` rather than falling back.
- **String-only surface.** No `byte[]` overloads — a caller already holding bytes converts to a string first.
