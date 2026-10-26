# DKNet.SlimBus.Generators

Moved to [DKNet.SlimBus.Generators documentation](../DKNet.SlimBus.Generators/index.md).

A test (`NamingRuleDocumentationTests`) reads the two blocks below from this file; do not edit or delete them. The readable worked example is on [Name requests and routes](../DKNet.SlimBus.Generators/name-requests-and-routes.md).

```csharp crud-naming-example
using System;
using DKNet.EfCore.Abstractions.Attributes;
using DKNet.EfCore.Abstractions.Entities;
using DKNet.EfCore.DtoGenerator;

namespace MyDomain
{
    public class Product : IEntity<Guid>
    {
        [CrudCreate]
        public Product(string name, decimal price)
        {
            Name = name;
            Price = price;
        }

        [CrudUpdate]
        public void Rename(string name) => Name = name;

        [CrudUpdate]
        public void ChangePrice(decimal price) => Price = price;

        public Guid Id { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public decimal Price { get; private set; }
    }
}

namespace MyApi
{
    [GenerateDto(typeof(MyDomain.Product))]
    public partial record ProductDto;
}
```

```text crud-naming-routes
GetById
GetList
Create
Delete
Rename
ChangePrice
```
