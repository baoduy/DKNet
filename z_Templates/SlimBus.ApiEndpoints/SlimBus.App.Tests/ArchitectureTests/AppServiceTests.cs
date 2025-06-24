using FluentValidation;
using SlimBus.AppServices;
using SlimMessageBus;

namespace SlimBus.App.Tests.ArchitectureTests;

public class AppServiceTests
{
    [Fact]
    public void AllHandlerClassesShouldBeInternalAndSealed()
    {
        // Adjust the assembly name if needed
        var types = NetArchTest.Rules.Types.InAssembly(typeof(AppSetup).Assembly);

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
    public void AllValidatorClassesShouldBeInternalAndSealed()
    {
        // Adjust the assembly name if needed
        var types = NetArchTest.Rules.Types.InAssembly(typeof(AppSetup).Assembly);

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
}