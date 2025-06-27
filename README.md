# DKNet Framework

[![codecov](https://codecov.io/github/baoduy/DKNet/graph/badge.svg?token=xtNN7AtB1O)](https://codecov.io/github/baoduy/DKNet)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/DKNet.Fw.Extensions)](https://www.nuget.org/packages/DKNet.Fw.Extensions/)
[![CodeQL Advanced](https://github.com/baoduy/DKNet/actions/workflows/codeql.yml/badge.svg)](https://github.com/baoduy/DKNet/actions/workflows/codeql.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=baoduy_DKNet&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=baoduy_DKNet)

![codeCove](https://codecov.io/gh/baoduy/DKNet/graphs/sunburst.svg?token=xtNN7AtB1O)

## Overview

**DKNet Framework** is a comprehensive collection of .NET libraries designed to enhance and simplify enterprise application development using **SlimMessageBus as a MediatR alternative** with **Domain-Driven Design (DDD)** principles. The framework provides robust foundations for building secure, scalable, and maintainable APIs while promoting clean architecture patterns and separation of concerns.

All projects in this framework serve as reference implementations and template foundations to help developers understand how to design their APIs securely and implement modern architectural patterns effectively.

---

## 🚀 Key Features

- **SlimMessageBus Integration**: Lightweight alternative to MediatR for CQRS and messaging patterns
- **Domain-Driven Design**: Full DDD implementation with aggregates, entities, and domain events
- **Clean Architecture**: Layered architecture with strict separation of concerns
- **EF Core Extensions**: Comprehensive Entity Framework Core enhancements and patterns
- **Security-First**: Built-in security patterns and data authorization mechanisms
- **Template Projects**: Ready-to-use project templates for rapid development
- **Aspire Integration**: .NET Aspire hosting extensions for cloud-native applications
- **Architectural Governance**: Automated enforcement of architectural rules using ArchUnitNET

---

## 📁 Project Structure

### Core Framework

- **[DKNet.Fw.Extensions](Core/DKNet.Fw.Extensions)** - Framework-level extensions and utilities

### Entity Framework Core Extensions

- **[DKNet.EfCore.Abstractions](EfCore/DKNet.EfCore.Abstractions)** - Core abstractions and interfaces
- **[DKNet.EfCore.DataAuthorization](EfCore/DKNet.EfCore.DataAuthorization)** - Data authorization and access control
- **[DKNet.EfCore.Events](EfCore/DKNet.EfCore.Events)** - Domain event handling and dispatching
- **[DKNet.EfCore.Extensions](EfCore/DKNet.EfCore.Extensions)** - EF Core functionality enhancements
- **[DKNet.EfCore.Hooks](EfCore/DKNet.EfCore.Hooks)** - Lifecycle hooks for EF Core operations
- **[DKNet.EfCore.Relational.Helpers](EfCore/DKNet.EfCore.Relational.Helpers)** - Relational database utilities
- **[DKNet.EfCore.Repos](EfCore/DKNet.EfCore.Repos)** - Repository pattern implementations
- **[DKNet.EfCore.Repos.Abstractions](EfCore/DKNet.EfCore.Repos.Abstractions)** - Repository abstractions

### Messaging & CQRS

- **[DKNet.SlimBus.Extensions](SlimBus/DKNet.SlimBus.Extensions)** - SlimMessageBus extensions for EF Core
- **DKNet.EfCore.SlimBus.Events** - SlimMessageBus event integration for EF Core

### Service Layer

- **[DKNet.Svc.BlobStorage.Abstractions](Services/DKNet.Svc.BlobStorage.Abstractions)** - File storage service abstractions
- **[DKNet.Svc.BlobStorage.AwsS3](Services/DKNet.Svc.BlobStorage.AwsS3)** - AWS S3 storage adapter
- **[DKNet.Svc.BlobStorage.AzureStorage](Services/DKNet.Svc.BlobStorage.AzureStorage)** - Azure Blob storage adapter
- **[DKNet.Svc.BlobStorage.Local](Services/DKNet.Svc.BlobStorage.Local)** - Local file system storage
- **[DKNet.Svc.Transformation](Services/DKNet.Svc.Transformation)** - Data transformation services

### Cloud-Native & Hosting

- **[Aspire.Hosting.ServiceBus](Aspire/Aspire.Hosting.ServiceBus)** - .NET Aspire Service Bus hosting extensions

### Templates

- **[SlimBus.ApiEndpoints](z_Templates/SlimBus.ApiEndpoints)** - Complete API template using SlimMessageBus

---

## 🏗️ Architecture Overview

### Domain-Driven Design Implementation

The framework implements a full DDD approach with:

![Diagram](Diagram.png)

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   API Layer     │    │  Application    │    │   Domain        │
│                 │    │   Services      │    │                 │
│ • Controllers   │◄──►│ • Commands      │◄──►│ • Entities      │
│ • Endpoints     │    │ • Queries       │    │ • Aggregates    │
│ • Validation    │    │ • Events        │    │ • Value Objects │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Infrastructure  │    │   SlimBus       │    │   EF Core       │
│                 │    │                 │    │                 │
│ • Repositories  │    │ • Message Bus   │    │ • DbContext     │
│ • External APIs │    │ • Event Handlers│    │ • Change Tracking│
│ • File Storage  │    │ • CQRS Pipeline │    │ • Interceptors  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Documentation

Refer our wiki for [details here](https://baoduy.github.io/DKNet/)

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

---

## 🙏 Acknowledgments

- **SlimMessageBus Team**: For providing an excellent messaging framework
- **Entity Framework Team**: For the robust ORM foundation
- **Domain-Driven Design Community**: For architectural guidance and patterns
- **Contributors**: All developers who have contributed to this framework
