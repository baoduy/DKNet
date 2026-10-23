# SHA content hashing

Part of [DKNet.Svc.Encryption](index.md). `IShaHashing` computes and verifies SHA-256/512 digests over UTF-8 input,
returning hexadecimal strings.

```csharp
using DKNet.Svc.Encryption.Hashing;

IShaHashing shaHashing = new ShaHashing();
var hash = shaHashing.ComputeSha256("hello world");
var matches = shaHashing.VerifySha256("hello world", hash);
Console.WriteLine(matches); // True
```

Output is lower-case hex unless `upperCase` is set. Verification is hex-only (`expectedHex`), constant-time, and
returns `false` — instead of throwing — when the expected value isn't valid hex. Empty string input is allowed;
`null` throws `ArgumentNullException`.

![VerifySha256 recomputes the hash and compares the decoded bytes with FixedTimeEquals; equal bytes return true, unequal bytes or invalid hex return false without throwing.](diagrams/sha-content-hashing.svg)

The diagram cannot show `ComputeSha256`/`ComputeSha512` used on their own (without verification) — that path is the
first half of the diagram: UTF-8 bytes in, hex string out, no comparison involved.

## ⚙️ Configuration reference

| Member | Parameter | Default | Effect |
|---|---|---|---|
| `ComputeSha256`/`ComputeSha512` | `upperCase` | `false` | `true` returns upper-case hex. |
| `VerifySha256`/`VerifySha512` | `ignoreCase` | `true` | No effect — comparison runs on decoded bytes, which are already case-insensitive. |

`IShaHashing` has no DI-level configuration: `AddEncryptionServices()` registers it as transient with no arguments.

## ⚠️ Gotchas & limits

- **`ignoreCase` on `VerifySha256`/`VerifySha512` is a no-op.** Comparison is done on decoded bytes, so case never
  matters — don't read the parameter as a behavior switch.
- **`IShaHashing` is not `IDisposable`.** It's a stateless wrapper over the static `System.Security.Cryptography`
  hash APIs — no `using` needed, unlike `IAesGcmEncryption`/`IRsaEncryption`.
- **No streaming or `byte[]` surface.** Input and output are both strings; a large payload is hashed as one UTF-8
  conversion.
