using DKNet.EfCore.DtoEntities;

namespace DKNet.EfCore.DtoGenerator.Tests.Features;

/// <summary>
/// DTO that uses only global exclusions (no local Exclude parameter).
/// Global exclusions should be applied automatically.
/// </summary>
[GenerateDto(typeof(GlobalExclusionTestEntity))]
public partial record GlobalExclusionTestDto;
