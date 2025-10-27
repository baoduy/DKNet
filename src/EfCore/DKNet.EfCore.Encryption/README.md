# DKNet.EfCore.Encryption

*A secure column encryption library for Entity Framework Core using AES-GCM.*

> **Note:** This project is a clone of [efeynr/EfCore.ColumnEncryption](https://github.com/efeynr/EfCore.ColumnEncryption/blob/master/README.md) and is being actively maintained and improved at [baoduy/DKNet/src/EfCore/DKNet.EfCore.Encryption](https://github.com/baoduy/DKNet/src/EfCore/DKNet.EfCore.Encryption).

## Table of Contents

- [Introduction](#introduction)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
  - [Package Manager](#package-manager)
  - [.NET CLI](#net-cli)
  - [Package Reference](#package-reference)
- [Getting Started](#getting-started)
  - [Setup](#setup)
  - [Usage](#usage)
- [Example](#example)
- [Limitations](#limitations)
- [Security Considerations](#security-considerations)
- [License](#license)
- [Contact](#contact)
- [FAQ](#faq)
- [Feedback](#feedback)
- [Notes](#notes)
- [Additional Resources](#additional-resources)

---

## Introduction

`DKNet.EfCore.Encryption` is a lightweight and secure library that provides column-level encryption for Entity Framework Core applications using the AES-GCM encryption algorithm. It allows you to protect sensitive data such as personal information, passwords, and financial data within your database columns without significant changes to your existing codebase.

---

## Features

- **Transparent Encryption and Decryption**: Automatically encrypts data before saving to the database and decrypts data when reading from the database.
- **AES-GCM Encryption**: Utilizes AES-GCM, a robust encryption algorithm that provides both confidentiality and integrity.
- **Easy Integration**: Minimal configuration required to start encrypting your data.
- **Support for Nullable Strings**: Handles `null` and empty strings gracefully.
- **Database-Agnostic**: Works with any database provider supported by Entity Framework Core.

---

## Prerequisites

- **.NET 8.0** or higher
- **Entity Framework Core 8.0.8** or higher

---

## Installation

You can install the `DKNet.EfCore.Encryption` package via NuGet Package Manager, .NET CLI, or by editing your project file.

### Package Manager

```powershell
Install-Package DKNet.EfCore.Encryption
```
### .NET CLI
```powershell
dotnet add package DKNet.EfCore.Encryption
```
### Package Reference
Add the following line to the .csproj file of your project
```xml
<PackageReference Include="DKNet.EfCore.Encryption" Version="latest" />
```

## Getting Started

### Setup

To start using DKNet.EfCore.Encryption, you need to configure your Entity Framework Core DbContext to use the library and provide an encryption key.

1. **Create an Encryption Provider**

```csharp
using EfCore.ColumnEncryption.Encryption;

// Your encryption key must be 16, 24, or 32 bytes for AES
byte[] encryptionKey = Convert.FromBase64String("Base64EncodedKeyHere");

var encryptionProvider = new AesGcmColumnEncryptionProvider(encryptionKey);
```

> Note: Ensure that you securely manage your encryption key. Do not hard-code it in your source code or expose it in version control systems.

2. **Configure Your DbContext**

```csharp
using EfCore.ColumnEncryption.Extensions;
using EfCore.ColumnEncryption.Interfaces;

public class ApplicationDbContext : DbContext
{
    private readonly IColumnEncryptionProvider _encryptionProvider;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IColumnEncryptionProvider encryptionProvider)
        : base(options)
    {
        _encryptionProvider = encryptionProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply encryption to properties marked with [Encrypted]
        modelBuilder.UseColumnEncryption(_encryptionProvider);
    }

    public DbSet<Customer> Customers { get; set; }
}
```

3. **Update Dependency Injection Configuration**

```csharp
services.AddSingleton<IColumnEncryptionProvider>(provider =>
{
    byte[] encryptionKey = Convert.FromBase64String("YourBase64EncodedKeyHere");
    return new AesGcmColumnEncryptionProvider(encryptionKey);
});

services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    options.UseSqlServer("Your DbConnection credentials");
});
```

### Usage

1. **Annotate Your Model Properties**

Use the `[Encrypted]` attribute on any properties that you wish to encrypt.

```csharp
using EfCore.ColumnEncryption.Attributes;

public class Customer
{
    public int Id { get; set; }

    [Encrypted]
    public string? CreditCardNumber { get; set; }

    [Encrypted]
    public string SocialSecurityNumber { get; set; }

    public string Name { get; set; }
}
```

2. **Perform Data Operations as Usual**

You can perform CRUD operations without any additional code. The library handles encryption and decryption automatically.

```csharp
var customer = new Customer
{
    Name = "John Doe",
    CreditCardNumber = "4111111111111111",
    SocialSecurityNumber = "123-45-6789"
};

_context.Customers.Add(customer);
await _context.SaveChangesAsync(); // Encrypts columns marked with the [Encrypted] attribute before saving them to the database

var storedCustomer = await _context.Customers.FindAsync(customer.Id);
Console.WriteLine(storedCustomer.CreditCardNumber); // Decrypted value
```


## Example

Here's a complete example demonstrating how to set up and use DKNet.EfCore.Encryption in an ASP.NET Core application.

**Program.cs**

```csharp
using EfCore.ColumnEncryption.Encryption;
using EfCore.ColumnEncryption.Interfaces;

var builder = WebApplication.CreateBuilder(args);



// Register the encryption provider
builder.Services.AddSingleton<IColumnEncryptionProvider>(
    new AesGcmColumnEncryptionProvider(Encoding.UTF8.GetBytes("yourEncryptionKeyInHere")));

// Register the DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer("connection string");
});

// ...

var app = builder.Build();

// ...

app.Run();

```

**ApplicationDbContext.cs**

```csharp
using EfCore.ColumnEncryption.Extensions;
using EfCore.ColumnEncryption.Interfaces;

public class ApplicationDbContext : DbContext
{
    private readonly IColumnEncryptionProvider _encryptionProvider;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IColumnEncryptionProvider encryptionProvider)
        : base(options)
    {
        _encryptionProvider = encryptionProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseColumnEncryption(_encryptionProvider);
    }

    public DbSet<Customer> Customers { get; set; }
}
```

**Customer.cs**

```csharp
using EfCore.ColumnEncryption.Attributes;

public class Customer
{
    public int Id { get; set; }

    [Encrypted]
    public string? CreditCardNumber { get; set; }

    [Encrypted]
    public string? SocialSecurityNumber { get; set; }

    public string? Name { get; set; }
}
```

**Usage in a Controller**

```csharp
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;

    public CustomersController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Create()
    {
        var customer = new Customer
        {
            Name = "Jane Doe",
            CreditCardNumber = "5555555555554444",
            SocialSecurityNumber = "987-65-4321"
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return View();
    }

    public async Task<IActionResult> Details(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        return View(customer);
    }
}
```

## Limitations

- **Data Length**: Encrypted data will be longer than the original plaintext. Ensure that your database columns are sized appropriately to store the encrypted data.
- **Performance Overhead**: Encryption and decryption introduce computational overhead. Performance impact is minimal for small amounts of data but consider benchmarking for large-scale applications.
- **Data Migrations**: When adding encryption to existing data, you'll need to migrate and encrypt existing plaintext data.

## Security Considerations

- **Key Security**: Protect your encryption key using secure key management practices.
- **Key Rotation**: Plan for key rotation and re-encryption strategies.

## Contributing

Contributions are welcome!

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contact

- **Author**: Efe Yener
- **Email**: efeyener6132@gmail.com
- **GitHub**: [efeynr](https://github.com/efeynr)


## FAQ

1. **Can I use this library with databases other than SQL Server?**
   Yes, DKNet.EfCore.Encryption is database-agnostic and should work with any database provider supported by Entity Framework Core.

2. **Does this library support encryption of data types other than strings?**
   Currently, the library focuses on encrypting string properties. Support for other data types may be added in future releases.

3. **How secure is the encryption provided by this library?**
   The library uses AES-GCM, which is a widely accepted and secure encryption algorithm. However, security also depends on how you manage your encryption keys and the overall security practices of your application.

## Feedback

If you encounter any issues, have questions, or want to suggest new features, please open an issue on the GitHub repository.

## Notes

- **Secure Key Management**: Do not commit your encryption keys or any sensitive information to version control.
- **Dependencies**: Regularly update your dependencies to include the latest security patches.

## Additional Resources

- [Official Documentation: Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [AES-GCM Information](https://en.wikipedia.org/wiki/Galois/Counter_Mode)

Thank you for using DKNet.EfCore.Encryption!
