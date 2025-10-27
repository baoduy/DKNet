using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features;

[GenerateDto(typeof(Person),
    Include =
    [
        nameof(Person.Id),
        nameof(Person.FirstName),
        nameof(Person.LastName),
        nameof(Person.Age)
    ])]
public partial record PersonSummaryDto;