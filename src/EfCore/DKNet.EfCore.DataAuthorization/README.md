# DKNet.WfCore.DataAuthorization

## Introduction
DKNet WfCore Data Authorization is a module designed to manage data access control within workflow applications, ensuring that users only interact with authorized data.

## Features
- **Role-Based Access Control (RBAC)**: Define roles and permissions to control data access.
- **Integration with DKNet WfCore**: Seamless integration with the broader DKNet WfCore framework.
- **Fine-Grained Authorization**: Granular control over data operations.
- **Auditing/Logging**: Track authorization events for compliance and debugging.
- **Customizable Policies**: Define custom rules based on specific requirements.

---

## Getting Started
### Prerequisites
- .NET Core 3.1 or later

### Installation
Install the package using NuGet Package Manager:
```bash
Install-Package DKNet.WfCore.DataAuthorization -Version x.x.x
```
Or via dotnet CLI:
```bash
dotnet add package DKNet.WfCore.DataAuthorization --version x.x.x
```

---

## Configuration
Modify `appsettings.json` to integrate authorization services:
```json
{
  "DataAuthorization": {
    "DefaultRole": "guest",
    "Roles": {
      "admin": {
        "AllowedResources": ["*"]
      },
      "user": {
        "AllowedResources": ["api/*/read"]
      }
    }
  }
}
```

## Usage
Inject `IDataAuthorizationService` into your controllers:
```csharp
public class UserController : ControllerBase
{
    private readonly IDataAuthorizationService _authorizationService;

    public UserController(IDataAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    [Authorize]
    public IActionResult GetUserData()
    {
        // Use _authorizationService to check permissions
        return Ok();
    }
}
```

---

## Advanced Configuration
For custom policies, extend `IDataAuthorizationPolicy` and register them in your DI container:
```csharp
public class CustomDataPolicy : IDataAuthorizationPolicy
{
    public bool IsAuthorized(User user, Operation operation)
    {
        // Implement custom logic here
        return true;
    }
}
```

---

## Troubleshooting
Common issues include missing dependencies, misconfigured policies, and authorization failures. Visit our [FAQ](https://faq.DKNetwfcore.com) for solutions.

## Conclusion
We welcome your feedback and contributions to improve DKNet.WfCore.DataAuthorization. Join our community on GitHub or Discord for support and updates.
