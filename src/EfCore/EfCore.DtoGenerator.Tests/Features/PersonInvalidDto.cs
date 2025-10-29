using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features;

// This should generate a warning at compile time since both Include and Exclude are specified
// The generator should skip generation for this DTO
[GenerateDto(
    typeof(Person),
    Include = [nameof(Person.FirstName), nameof(Person.LastName)],
    Exclude = [nameof(Person.Age)])]
public record PersonInvalidDto
{
    #region Properties

    // Manually add a property to prevent empty record compilation error
    public string ManualProperty { get; init; } = string.Empty;

    #endregion
}