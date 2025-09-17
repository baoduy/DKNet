# DKNet.RandomCreator

A lightweight .NET library for generating random strings and character arrays, suitable for passwords, tokens, and other
random data needs. Uses cryptographically secure random number generation.

## Features

- Generate random strings or char arrays of any length
- Enforce minimum numbers and special characters
- Optionally restrict to alphabetic-only output
- Simple static API with flexible options
- .NET 9.0 compatible

## Installation

Add the NuGet package to your project:

```
dotnet add package DKNet.RandomCreator
```

## Usage

```csharp
using DKNet.RandomCreator;

// Generate a random string (default length 25)
string randomString = RandomCreators.NewString();

// Generate a random string of length 32, with at least 4 numbers and 2 special characters
var options = new StringCreatorOptions { MinNumbers = 4, MinSpecials = 2 };
string strongPassword = RandomCreators.NewString(32, options);

// Generate a random alphabetic-only string of length 16
var alphaOptions = new StringCreatorOptions { AlphabeticOnly = true };
string alphaString = RandomCreators.NewString(16, alphaOptions);

// Generate a random char array with options
char[] randomChars = RandomCreators.NewChars(20, options);
```

## API

- `RandomCreators.NewString(int length = 25, StringCreatorOptions? options = null)`: Returns a random string with optional constraints.
- `RandomCreators.NewChars(int length = 25, StringCreatorOptions? options = null)`: Returns a random char array with optional constraints.
- `StringCreatorOptions`: Options to control alphabetic-only, minimum numbers, and minimum special characters.

## License

MIT © 2026 drunkcoding

## Repository

[https://github.com/baoduy/DKNet](https://github.com/baoduy/DKNet)

## Contributing

Pull requests and issues are welcome!
