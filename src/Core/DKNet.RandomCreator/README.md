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

## Documentation

| Guide | Read when |
|---|---|
| [Package overview](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.RandomCreator/index.md) | Start with architecture and the end-to-end flow. |
| [Generate strings and mutable character buffers](https://github.com/baoduy/DKNet/blob/main/docs/DKNet.RandomCreator/generate-random-text.md) | Choose string or mutable-buffer output with fixed digit and symbol counts. |
