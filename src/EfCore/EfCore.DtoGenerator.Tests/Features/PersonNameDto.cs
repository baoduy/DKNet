using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features;

[GenerateDto(typeof(Person), Include = [nameof(Person.FirstName), nameof(Person.LastName)])]
public partial record PersonNameDto;