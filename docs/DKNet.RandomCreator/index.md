# DKNet.RandomCreator

Random string and character-buffer generation for .NET developers, using System.Security.Cryptography.RandomNumberGenerator.

## ✨ Why use it?

- Generate text without writing character-selection and shuffle loops.
- Choose fixed digit and symbol counts while filling the remaining positions with letters.
- Receive a mutable character array when the caller needs to clear the returned buffer.

## 🚀 Quick Start

Use a .NET 10 console project. Install the package, then replace `Program.cs` with the example.

```bash
dotnet new console -f net10.0 -n CoreDemo
cd CoreDemo
dotnet add package DKNet.RandomCreator
```

```csharp
using System;
using DKNet.RandomCreator;

var text = RandomCreators.NewString(32,
    new StringCreatorOptions { MinNumbers = 4, MinSpecials = 2 });
Console.WriteLine(text.Length); // 32
```

Run with `dotnet run`.

## 🏗️ Architecture

![The caller invokes RandomCreators, which creates StringCreator to draw and shuffle characters with the .NET RandomNumberGenerator APIs.](diagrams/components.svg)

The public static methods create a fresh internal `StringCreator` for each synchronous call. Selection and shuffling use the .NET `RandomNumberGenerator` APIs; there is no service registration, store or background worker. The project declares no direct package or project references; repository-wide build settings add analyzer tooling.

## 🔄 How it works

![RandomCreators validates the requested length and quota sum, draws digits and symbols, fills remaining positions with letters, and shuffles the buffer.](diagrams/randomcreator-string-composition.svg)

With the default length of 25 and zero quotas, output contains only letters. For nonnegative quotas, the digit and symbol counts are exact, and their sum must be less than the requested length. Invalid length or quota sum throws `ArgumentException`; detailed limits and both output forms are covered in the feature page.

## 🧩 Features

- [Generate strings and mutable character buffers](generate-random-text.md) — Choose string or mutable-buffer output with fixed digit and symbol counts.

## 🔗 Related packages

- [DKNet.Fw.Extensions](../DKNet.Fw.Extensions/index.md) — reach for it when you need reflection, type discovery or value helpers.
- [DKNet.Svc.Encryption](../DKNet.Svc.Encryption/index.md) — reach for it when you need encryption, hashes or HMAC rather than random text.
