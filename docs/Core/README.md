# Core Framework

The Core Framework provides foundational utilities and extensions that support all other DKNet components. These cross-cutting concerns are essential for building robust, maintainable applications.

## Components

- [DKNet.Fw.Extensions](./DKNet.Fw.Extensions.md) - Framework-level extensions and utilities

## Architecture Role

The Core Framework sits at the foundation of the DKNet architecture, providing cross-cutting concerns that are used across all layers:

```
┌─────────────────────────────────────────────────────────────────┐
│                        🌐 Presentation Layer                    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                     🎯 Application Layer                        │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                      💼 Domain Layer                           │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🗄️ Infrastructure Layer                      │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│              ⚙️ Core Framework (Cross-cutting)                │
│                                                                 │
│  • String Extensions        • Type Extensions                   │
│  • DateTime Extensions      • Enum Utilities                    │
│  • Encryption Services      • DI Container Extensions           │
│  • Async Extensions         • Property Helpers                  │
└─────────────────────────────────────────────────────────────────┘
```

## Key Features

### Foundation Services
- **Extension Methods**: Comprehensive set of extension methods for common .NET types
- **Type System Utilities**: Advanced type checking and manipulation utilities
- **Dependency Injection**: Enhanced service registration and keyed service support

### Security & Encryption
- **String Encryption**: Secure string encryption and decryption utilities
- **Hashing Services**: Consistent hashing algorithms for data integrity

### Data Processing
- **Async Enumerables**: Enhanced async enumerable processing
- **String Manipulation**: Advanced string processing and validation
- **Enum Utilities**: Rich enum information extraction and processing

## Design Principles

The Core Framework follows these key principles:

1. **Non-intrusive**: Extensions that don't change existing behavior
2. **Performance-oriented**: Optimized implementations for common operations
3. **Dependency-minimal**: Minimal external dependencies to reduce coupling
4. **Cross-platform**: Compatible across different .NET platforms
5. **Testable**: All components are easily testable and mockable