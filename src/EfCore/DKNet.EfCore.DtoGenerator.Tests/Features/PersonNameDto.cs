using DKNet.EfCore.DtoEntities;

namespace DKNet.EfCore.DtoGenerator.Tests.Features;

[GenerateDto(typeof(Person), Include = [nameof(Person.FirstName), nameof(Person.LastName)])]
public partial record PersonNameDto;