using System.Reflection;
using EfCore.DtoGenerator.Tests.ProjectWide.Features;
using Shouldly;

namespace EfCore.DtoGenerator.Tests.ProjectWide;

public class ProjectWideIgnoreComplexTypeTests
{
    private static PropertyInfo? FindProperty<TDto>(string name) =>
        typeof(TDto).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

    [Fact]
    public void CustomerProjectWideDto_IncludesNavigationProperties_WhenProjectWideFalse()
    {
        FindProperty<Features.CustomerProjectWideDto>(nameof(CustomerProjectWideDto.Orders)).ShouldNotBeNull();
        FindProperty<Features.CustomerProjectWideDto>(nameof(CustomerProjectWideDto.PrimaryAddress)).ShouldNotBeNull();
    }

    [Fact]
    public void CustomerProjectWideDto_IncludesScalarAndInheritedProperties()
    {
        FindProperty<Features.CustomerProjectWideDto>(nameof(CustomerProjectWideDto.Email)).ShouldNotBeNull();
        FindProperty<Features.CustomerProjectWideDto>(nameof(CustomerProjectWideDto.Name)).ShouldNotBeNull();
    }

    [Fact]
    public void CustomerExplicitIgnoreDto_OverridesProjectWideFlag_AndExcludesNavigationProperties()
    {
        FindProperty<Features.CustomerExplicitIgnoreDto>("Orders").ShouldBeNull();
        FindProperty<Features.CustomerExplicitIgnoreDto>("PrimaryAddress").ShouldBeNull();
        FindProperty<Features.CustomerExplicitIgnoreDto>(nameof(CustomerExplicitIgnoreDto.Email)).ShouldNotBeNull();
        FindProperty<Features.CustomerExplicitIgnoreDto>(nameof(CustomerExplicitIgnoreDto.Name)).ShouldNotBeNull();
    }
}