using DKNet.EfCore.Extensions.Configurations;
using DKNet.EfCore.Repos.Abstractions;
using FluentValidation;
using NetArchTest.Rules;
using SlimBus.Infra.Extensions;
using SlimMessageBus;

namespace SlimBus.App.Tests.Architecture;

public class InfraTests
{
    #region Methods

    [Fact]
    public void AllEfConfigClassesShouldBeInternalAndSealed()
    {
        // Adjust the assembly name if needed
        var types = Types.InAssembly(typeof(InfraSetup).Assembly);

        var result = types
            .That()
            .AreClasses()
            .And()
            .Inherit(typeof(IEntityTypeConfiguration<>))
            .Should().NotBePublic()
            .And().BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"These EfCore Config classes should be sealed and internal: {string.Join(", ", (result.FailingTypes ?? []).Select(t => t.FullName))}");
    }

    [Fact]
    public void AllHandlerClassesShouldBeInternalAndSealed()
    {
        // Adjust the assembly name if needed
        var types = Types.InAssembly(typeof(InfraSetup).Assembly);

        var result = types
            .That()
            .AreClasses()
            .And().ImplementInterface(typeof(IRequestHandler<>))
            .Or().ImplementInterface(typeof(IRequestHandler<,>))
            .Or().ImplementInterface(typeof(IConsumer<>))
            .Should().NotBePublic()
            .And().BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"These handler classes should be sealed and internal: {string.Join(", ", (result.FailingTypes ?? []).Select(t => t.FullName))}");
    }

    [Fact]
    public void AllRepoClassesShouldBeInternalAndSealed()
    {
        // Adjust the assembly name if needed
        var types = Types.InAssembly(typeof(InfraSetup).Assembly);

        var result = types
            .That()
            .AreClasses()
            .And().ImplementInterface(typeof(IRepository<>))
            .Or().ImplementInterface(typeof(IWriteRepository<>))
            .Should().NotBePublic()
            .And().BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"These EfCore Config classes should be sealed and internal: {string.Join(", ", (result.FailingTypes ?? []).Select(t => t.FullName))}");
    }

    [Fact]
    public void AllSeedingDataClassesShouldBeInternalAndSealed()
    {
        // Adjust the assembly name if needed
        var types = Types.InAssembly(typeof(InfraSetup).Assembly);

        var result = types
            .That()
            .AreClasses()
            .And().ImplementInterface(typeof(IDataSeedingConfiguration))
            .Should().NotBePublic()
            .And().BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"These EfCore Config classes should be sealed and internal: {string.Join(", ", (result.FailingTypes ?? []).Select(t => t.FullName))}");
    }

    [Fact]
    public void AllValidatorClassesShouldBeInternalAndSealed()
    {
        // Adjust the assembly name if needed
        var types = Types.InAssembly(typeof(InfraSetup).Assembly);

        var result = types
            .That()
            .AreClasses()
            .And()
            .Inherit(typeof(AbstractValidator<>))
            .Should().NotBePublic()
            .And().BeSealed()
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(
            $"These handler classes should be sealed and internal: {string.Join(", ", (result.FailingTypes ?? []).Select(t => t.FullName))}");
    }

    #endregion
}