using DKNet.EfCore.DtoEntities;

namespace DKNet.EfCore.DtoGenerator.Tests.Features;

/// <summary>
///     DTO that uses Include parameter to override global exclusions.
///     Only the included properties should be present, ignoring global exclusions.
/// </summary>
[GenerateDto(typeof(GlobalExclusionTestEntity),
    Include =
    [
        nameof(GlobalExclusionTestEntity.Id),
        nameof(GlobalExclusionTestEntity.Name),
        nameof(GlobalExclusionTestEntity.CreatedAt)
    ])]
public partial record IncludeOverridesGlobalExclusionTestDto;