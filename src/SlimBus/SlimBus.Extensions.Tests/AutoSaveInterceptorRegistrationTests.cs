using DKNet.SlimBus.Extensions.Interceptors;
using SlimMessageBus.Host.Interceptor;

namespace SlimBus.Extensions.Tests;

/// <summary>
///     DRK-2483: <c>AddSlimBusEfCoreInterceptor&lt;TDbContext&gt;()</c> must register the auto-save interceptor
///     exactly once per service collection, no matter which other <see cref="IRequestHandlerInterceptor{TRequest,TResponse}" />
///     registrations (open, closed or keyed) the host made before or after it.
/// </summary>
public class AutoSaveInterceptorRegistrationTests
{
    #region Methods

    private static ServiceDescriptor[] AutoSaveDescriptors(IServiceCollection services) =>
        services.Where(d => d.ServiceType == typeof(IRequestHandlerInterceptor<,>)
                            && !d.IsKeyedService
                            && d.ImplementationType == typeof(EfAutoSavePostInterceptor<,>))
            .ToArray();

    private static IServiceCollection AddSecondDbContext(IServiceCollection services) =>
        services.AddDbContext<SecondTestDbContext>(b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()));

    private static async Task SendWriteAndAssertSavedOnce(ServiceProvider provider)
    {
        var result = await provider.GetRequiredService<IMessageBus>().Send(new SecondTestRequest { Name = "HBD" });

        result.IsSuccess.ShouldBeTrue();
        provider.GetRequiredService<SecondTestDbContext>().SaveCount.ShouldBe(1);
    }

    [Fact]
    public async Task AddSlimBusEfCoreInterceptor_OpenGenericInterceptorRegisteredBefore_StillAutoSaves()
    {
        await using var provider = RegistrySafetyTests.BuildBusProvider(services => AddSecondDbContext(services)
            .AddScoped(typeof(IRequestHandlerInterceptor<,>), typeof(PassThroughInterceptor<,>))
            .AddSlimBusEfCoreInterceptor<SecondTestDbContext>());

        await SendWriteAndAssertSavedOnce(provider);
    }

    [Fact]
    public async Task AddSlimBusEfCoreInterceptor_OpenGenericInterceptorRegisteredAfter_StillAutoSavesOnce()
    {
        await using var provider = RegistrySafetyTests.BuildBusProvider(services => AddSecondDbContext(services)
            .AddSlimBusEfCoreInterceptor<SecondTestDbContext>()
            .AddScoped(typeof(IRequestHandlerInterceptor<,>), typeof(PassThroughInterceptor<,>)));

        await SendWriteAndAssertSavedOnce(provider);
    }

    [Fact]
    public void AddSlimBusEfCoreInterceptor_OpenGenericInterceptorRegisteredBefore_AddsAutoSaveAfterIt()
    {
        var services = new ServiceCollection()
            .AddScoped(typeof(IRequestHandlerInterceptor<,>), typeof(PassThroughInterceptor<,>))
            .AddSlimBusEfCoreInterceptor<SecondTestDbContext>();

        AutoSaveDescriptors(services).Length.ShouldBe(1);
        // The competing registration is kept, and kept first.
        services.First(d => d.ServiceType == typeof(IRequestHandlerInterceptor<,>))
            .ImplementationType.ShouldBe(typeof(PassThroughInterceptor<,>));
    }

    [Fact]
    public void AddSlimBusEfCoreInterceptor_ClosedGenericInterceptorRegisteredBefore_AddsAutoSave()
    {
        var services = new ServiceCollection()
            .AddScoped<IRequestHandlerInterceptor<SecondTestRequest, FluentResults.IResultBase>,
                PassThroughInterceptor<SecondTestRequest, FluentResults.IResultBase>>()
            .AddSlimBusEfCoreInterceptor<SecondTestDbContext>();

        AutoSaveDescriptors(services).Length.ShouldBe(1);
        services.Count(d => d.ServiceType == typeof(IRequestHandlerInterceptor<SecondTestRequest, FluentResults.IResultBase>))
            .ShouldBe(1);
    }

    [Fact]
    public void AddSlimBusEfCoreInterceptor_KeyedOpenGenericInterceptorRegisteredBefore_AddsAutoSave()
    {
        var services = new ServiceCollection()
            .AddKeyedScoped(typeof(IRequestHandlerInterceptor<,>), "competing", typeof(PassThroughInterceptor<,>));

        Should.NotThrow(() => services.AddSlimBusEfCoreInterceptor<SecondTestDbContext>());

        AutoSaveDescriptors(services).Length.ShouldBe(1);
        services.Count(d => d.IsKeyedService && Equals(d.ServiceKey, "competing")).ShouldBe(1);
    }

    [Fact]
    public void AddSlimBusEfCoreInterceptor_CalledTwiceForSameDbContext_AddsOneAutoSaveDescriptor()
    {
        var services = new ServiceCollection()
            .AddSlimBusEfCoreInterceptor<SecondTestDbContext>()
            .AddSlimBusEfCoreInterceptor<SecondTestDbContext>();

        AutoSaveDescriptors(services).Length.ShouldBe(1);
    }

    [Fact]
    public void AddSlimBusEfCoreInterceptor_TwoDbContextTypes_AddsOneAutoSaveDescriptor()
    {
        var services = new ServiceCollection()
            .AddSlimBusEfCoreInterceptor<TestDbContext>()
            .AddSlimBusEfCoreInterceptor<SecondTestDbContext>();

        AutoSaveDescriptors(services).Length.ShouldBe(1);
    }

    #endregion

    // Nested and private so assembly scanning (AddServicesFromAssembly) never registers it on other buses.
    private sealed class PassThroughInterceptor<TRequest, TResponse> : IRequestHandlerInterceptor<TRequest, TResponse>
    {
        public Task<TResponse> OnHandle(TRequest request, Func<Task<TResponse>> next, IConsumerContext context) => next();
    }
}
