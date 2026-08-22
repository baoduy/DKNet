using EfCore.DtoGenerator.TestEntities;
using DKNet.EfCore.DtoGenerator;

namespace EfCore.DtoGenerator.Tests.Features;

/// <summary>
///     DRK-698 review fix regression: <c>Exclude</c> written as <c>new[] { nameof(...) }</c> (an
///     array-initializer, not a collection expression) must narrow the DTO exactly like a string literal.
/// </summary>
[GenerateDto(typeof(GlobalExclusionTestEntity), Exclude = new[] { nameof(GlobalExclusionTestEntity.IsActive) })]
public partial record NameofArrayExcludeTestDto;
