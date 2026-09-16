using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using Mapster;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace SlimBus.Extensions.Tests;

// A distinct closed generic of this per TMarker is a distinct DbContext-derived Type — the cheapest
// way to hand AddSlimBusEfCoreInterceptor<TDbContext>() a large, genuinely novel supply of registry
// keys without declaring hundreds of named classes by hand.
internal sealed class ThrowawayDbContext<TMarker> : DbContext;

/// <summary>
///     DRK-1343: the EF Core auto-save DbContext registry must live per service container, not in a
///     process-wide static, so concurrent provider construction never corrupts it and one provider
///     never sees another provider's DbContext types. Every provider here builds its own
///     <see cref="ServiceCollection" />/<see cref="ServiceProvider" /> on purpose: the shared
///     <see cref="Fixture" /> is what the process-wide registry made unsafe to use concurrently, and
///     widening it would hide the very defect these scenarios prove.
/// </summary>
public class RegistrySafetyTests
{
    #region Methods

    private static readonly MethodInfo RegisterThrowawayMethod = typeof(RegistrySafetyTests)
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(m => m.Name == nameof(RegisterThrowaway) && m.IsGenericMethodDefinition);

    // Every distinct TMarker closes ThrowawayDbContext<TMarker> into a brand-new Type, so each call
    // is a genuine registry mutation (not a no-op re-add of an already-known key).
    private static void RegisterThrowaway<TMarker>(IServiceCollection services) =>
        services.AddSlimBusEfCoreInterceptor<ThrowawayDbContext<TMarker>>();

    private static void RegisterThrowaway(IServiceCollection services, Type markerType) =>
        RegisterThrowawayMethod.MakeGenericMethod(markerType).Invoke(null, [services]);

    private static Type[] CreateMarkerTypes(int count)
    {
        var moduleBuilder = AssemblyBuilder
            .DefineDynamicAssembly(new AssemblyName($"Drk1343.Markers.{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("MarkersModule");

        return Enumerable.Range(0, count)
            .Select(i => moduleBuilder.DefineType($"Marker{i}", TypeAttributes.Public).CreateType())
            .ToArray();
    }

    private static ServiceProvider BuildBusProvider(
        Action<IServiceCollection> configureDbContexts, bool perMessageScope = false)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TypeAdapterConfig.GlobalSettings)
            .AddScoped<IMapper, ServiceMapper>();

        configureDbContexts(services);

        services.AddSlimMessageBus(mmb => mmb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(Fixture).Assembly)
            .AddChildBus(
                "ImMemory",
                me =>
                {
                    me.WithProviderMemory().AutoDeclareFrom(typeof(Fixture).Assembly);
                    if (perMessageScope) me.PerMessageScopeEnabled();
                }));

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddSlimBusEfCoreInterceptor_TwoIndependentProviders_EachAutoSavesOnlyOwnContext()
    {
        // Provider A only exists to register TestDbContext for auto-save on its OWN collection.
        // Against a process-wide registry this is what leaks typeof(TestDbContext) into every
        // provider built afterwards in this process.
        await using var providerA = new ServiceCollection()
            .AddDbContext<TestDbContext>(b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()))
            .AddSlimBusEfCoreInterceptor<TestDbContext>()
            .BuildServiceProvider();

        // Provider B has a TestDbContext registered too (e.g. used elsewhere in that host) but
        // never calls AddSlimBusEfCoreInterceptor<TestDbContext>() itself — only SecondTestDbContext
        // is opted into auto-save on Provider B's collection.
        await using var providerB = BuildBusProvider(services => services
            .AddDbContext<TestDbContext>(b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()))
            .AddDbContext<SecondTestDbContext>(b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()))
            .AddSlimBusEfCoreInterceptor<SecondTestDbContext>());

        var incidental = providerB.GetRequiredService<TestDbContext>();
        incidental.Entities.Add(new TestEntity { Name = "never-opted-in" });
        incidental.ChangeTracker.HasChanges().ShouldBeTrue();

        var bus = providerB.GetRequiredService<IMessageBus>();
        var result = await bus.Send(new SecondTestRequest { Name = "own" });

        result.IsSuccess.ShouldBeTrue();
        providerB.GetRequiredService<SecondTestDbContext>().SaveCount.ShouldBe(1);

        // registry_is_per_container: Provider B never registered TestDbContext for auto-save, so
        // its pending change must still be pending — a process-wide registry saves it anyway
        // because Provider A registered that type somewhere else in the process.
        incidental.ChangeTracker.HasChanges().ShouldBeTrue(
            "Provider B never opted TestDbContext into auto-save on its own collection");
    }

    [Fact]
    public async Task AddSlimBusEfCoreInterceptor_BuiltByManyProvidersConcurrently_DoesNotThrow()
    {
        var providerCount = Math.Max(4, Environment.ProcessorCount);
        const int typesPerProvider = 500;
        var markerTypes = CreateMarkerTypes(providerCount * typesPerProvider);
        var exceptions = new ConcurrentBag<Exception>();
        var providers = new ServiceProvider?[providerCount];

        await Task.WhenAll(Enumerable.Range(0, providerCount).Select(i => Task.Run(() =>
        {
            try
            {
                var services = new ServiceCollection();
                // Each provider registers many never-before-seen DbContext types so the shared
                // registry keeps mutating on every provider's thread for the whole race window,
                // instead of one Add() each that may never collide with another thread's.
                for (var j = 0; j < typesPerProvider; j++)
                    RegisterThrowaway(services, markerTypes[i * typesPerProvider + j]);
                providers[i] = services.BuildServiceProvider();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })));

        try
        {
            // concurrent_registration_does_not_throw: registering the interceptor from many
            // providers at once, each with its own never-before-seen DbContext type, must never
            // throw out of a shared, unsynchronised collection.
            exceptions.ShouldBeEmpty();
            providers.ShouldAllBe(p => p != null);
        }
        finally
        {
            foreach (var provider in providers) if (provider is not null) await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task OnHandle_RegistrationOnAnotherProviderWhileEnumerating_DoesNotThrow()
    {
        await using var provider = BuildBusProvider(services => services
            .AddDbContext<TestDbContext>(b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()))
            .AddSlimBusEfCoreInterceptor<TestDbContext>(), perMessageScope: true);
        var bus = provider.GetRequiredService<IMessageBus>();

        const int sendCount = 300;
        var registrationWorkerCount = Math.Max(2, Environment.ProcessorCount);

        // A large pre-generated pool so every background worker keeps making genuinely new
        // registry mutations (not no-op re-adds) for the whole race window below, and enough
        // parallel senders that many enumerations of the registry are in flight at once.
        var markerTypes = new ConcurrentQueue<Type>(CreateMarkerTypes(20_000));

        using var stop = new CancellationTokenSource();
        var registrationLoops = Enumerable.Range(0, registrationWorkerCount).Select(_ => Task.Run(() =>
        {
            var services = new ServiceCollection();
            while (!stop.IsCancellationRequested && markerTypes.TryDequeue(out var markerType))
                RegisterThrowaway(services, markerType);
        })).ToArray();

        Exception? observed = null;
        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, sendCount),
                new ParallelOptions { MaxDegreeOfParallelism = registrationWorkerCount },
                async (_, ct) =>
                {
                    var rs = await bus.Send(new TestNoResponseRequest { Name = "race" }, cancellationToken: ct);
                    rs.IsSuccess.ShouldBeTrue();
                });
        }
        catch (Exception ex)
        {
            observed = ex;
        }
        finally
        {
            stop.Cancel();
            await Task.WhenAll(registrationLoops);
        }

        // registration_while_enumerating_does_not_throw: reading the registry to auto-save on one
        // provider while another provider registers must not blow up an unsynchronised enumeration.
        observed.ShouldBeNull();
    }

    [Fact]
    public async Task AddSlimBusEfCoreInterceptor_CalledTwiceForSameDbContext_SavesOnce()
    {
        await using var provider = BuildBusProvider(services => services
            .AddDbContext<SecondTestDbContext>(b => b.UseInMemoryDatabase(Guid.NewGuid().ToString()))
            .AddSlimBusEfCoreInterceptor<SecondTestDbContext>()
            .AddSlimBusEfCoreInterceptor<SecondTestDbContext>());

        var bus = provider.GetRequiredService<IMessageBus>();
        var result = await bus.Send(new SecondTestRequest { Name = "HBD" });

        result.IsSuccess.ShouldBeTrue();
        // duplicate_registration_saves_once: registering the same TDbContext twice on one
        // collection must not translate into a double SaveChangesAsync per handled request.
        provider.GetRequiredService<SecondTestDbContext>().SaveCount.ShouldBe(1);
    }

    #endregion
}
