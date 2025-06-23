# WX.EfCore Solution

The **WX.EfCore** solution is a collection of libraries designed to enhance and simplify working with **Entity Framework Core (EF Core)**. It provides tools for extensions, hooks, and repository patterns to improve productivity, maintainability, and performance in EF Core-based applications.

This README provides an overview of the solution, excluding unit test projects, focusing on the main components: `Extensions`, `Hooks`, `Repos`, and `Repos.Abstractions`.

---

# Projects

## WX.Framework.Extensions
Provides additional framework-level extensions to complement the core functionality of the WX.EfCore libraries.

## WX.EfCore.Abstractions
Defines the core abstractions and interfaces used across the WX.EfCore libraries, ensuring consistency and interoperability.

## WX.EfCore.DataAuthorization
Provides data authorization mechanisms to control access to data entities based on user roles and permissions.

## WX.EfCore.Events
Implements event handling and dispatching capabilities to facilitate decoupled communication between different parts of the application.

## WX.EfCore.Extensions
Offers a set of useful extensions to enhance the functionality and usability of EF Core.

## WX.EfCore.Hooks
Introduces hooks that allow you to inject custom logic at various points in the EF Core lifecycle, such as before or after saving changes.

## WX.EfCore.MediatR.Events
Integrates MediatR with EF Core to support the publishing and handling of domain events.

## WX.EfCore.Relational.Helpers
Provides helper methods and utilities specifically for relational database operations in EF Core.

## WX.EfCore.Repos
Implements repository patterns to abstract data access logic, promoting a cleaner and more maintainable codebase.

## WX.EfCore.Repos.Abstractions
Defines the abstractions for repository patterns, ensuring a consistent approach to data access across different implementations.

## WX.EfCore.SlimBus.Events
Offers a lightweight event bus for handling events within the EF Core context, promoting a decoupled architecture.

## WX.MediatR.Extensions
Provides additional extensions for integrating MediatR with EF Core, enhancing the capabilities of domain event handling and dispatching.

## WX.SlimBus.Extensions
Offers a set of extensions for the SlimBus event bus, facilitating easier integration and usage within EF Core applications.

## WX.Services.FileStorage
Defines a service for managing file storage operations, abstracting the underlying storage mechanisms to provide a consistent interface.

## WX.Services.FileStorage.AwsS3Adapters
Implements adapters for integrating AWS S3 with the file storage service, enabling seamless file operations on AWS S3.

## WX.Services.FileStorage.AzureAdapters
Implements adapters for integrating Azure Blob Storage with the file storage service, enabling seamless file operations on Azure.

## WX.Services.Transformation
Provides services for transforming data between different formats or structures, supporting various transformation scenarios within the application.

---

# Templates
## Templates

### MediatR.ApiEndpoints
Provides a template for creating API endpoints that leverage MediatR for handling requests and responses. This template streamlines the process of setting up MediatR-based API endpoints, ensuring a consistent and maintainable approach to request handling within your application.

### SlimBus.ApiEndpoints
Offers a template for creating API endpoints that utilize the SlimBus event bus for handling events. This template simplifies the integration of SlimBus into your API, promoting a decoupled and event-driven architecture for handling API requests and responses.

## Contributing

Contributions are welcome! To contribute:

1. Fork the repository.
2. Create a feature branch for your changes.
3. Commit your changes with clear and descriptive messages.
4. Push your branch back to GitHub and create a Pull Request.

Please ensure that any new features or changes adhere to the existing coding standards and design principles of the project.

---

## License

This solution is licensed under the [MIT License](LICENSE).

---

## Acknowledgments

- Thanks to the **Entity Framework Core** team for providing a robust framework.
- Special thanks to contributors who have enhanced these libraries.