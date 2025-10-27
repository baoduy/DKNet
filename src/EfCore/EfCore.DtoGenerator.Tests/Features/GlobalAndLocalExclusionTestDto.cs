using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features;

/// <summary>
///     DTO that combines global exclusions with local Exclude parameter.
///     Both global and local exclusions should be applied.
/// </summary>
[GenerateDto(typeof(GlobalExclusionTestEntity), Exclude = [nameof(GlobalExclusionTestEntity.IsActive)])]
public partial record GlobalAndLocalExclusionTestDto;