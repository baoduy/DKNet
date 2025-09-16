# DKNet.RandomCreator

A lightweight .NET library for generating random strings and character arrays, suitable for passwords, tokens, and other
random data needs. Uses cryptographically secure random number generation.

## Features

- Generate random strings of any length
- Optionally include symbols for stronger passwords
- Simple static API
- .NET 9.0 compatible

## Installation

Add the NuGet package to your project:

```
dotnet add package DKNet.RandomCreator
```

## Usage

```csharp
using DKNet.RandomCreator;

// Generate a random string (default length 25, no symbols)
string randomString = RandomCreators.String();

// Generate a random string of length 32, including symbols
string strongPassword = RandomCreators.String(32, includeSymbols: true);

// Generate a random char array
char[] randomChars = RandomCreators.Chars(16);
```

## API

- `RandomCreators.String(int length = 25, bool includeSymbols = false)`: Returns a random string.
- `RandomCreators.Chars(int length = 25, bool includeSymbols = false)`: Returns a random char array.

## License

MIT © 2026 drunkcoding

## Repository

[https://github.com/baoduy/DKNet](https://github.com/baoduy/DKNet)

## Contributing

Pull requests and issues are welcome!

