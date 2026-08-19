using DKNet.EfCore.DataAuthorization.Internals;

namespace EfCore.DataAuthorization.Tests;

/// <summary>
///     Tests for <see cref="EfCoreDataAuthSetup" />'s duplicate-registration guard: repeated
///     <see cref="EfCoreDataAuthSetup.AddDataOwnerProvider{TDbContext,TProvider}" /> calls collapse to a single
///     <see cref="IDataOwnerProvider" /> registration, first-wins (DRK-466).
/// </summary>
public class DataAuthorizationSetupDedupTests
{
    #region Methods

    [Fact]
    public void AddDataOwnerProvider_CalledTwice_RegistersProviderOnlyOnce()
    {
        var services = new ServiceCollection();

        services
            .AddDataOwnerProvider<DddContext, TestDataKeyProvider>()
            .AddDataOwnerProvider<UnrestrictedDddContext, NonEmptyAccessibleKeysProvider>();

        services.Count(s => s.ServiceType == typeof(IDataOwnerProvider)).ShouldBe(1);
    }

    [Fact]
    public void AddDataOwnerProvider_CalledTwiceWithDifferentProviders_FirstProviderWins()
    {
        var services = new ServiceCollection();

        services
            .AddDataOwnerProvider<DddContext, TestDataKeyProvider>()
            .AddDataOwnerProvider<UnrestrictedDddContext, NonEmptyAccessibleKeysProvider>();

        using var provider = services.BuildServiceProvider();
        var registered = provider.GetRequiredService<IDataOwnerProvider>();

        registered.ShouldBeOfType<TestDataKeyProvider>();
    }

    [Fact]
    public void AddDataOwnerProvider_CalledTwiceWithDifferentDbContexts_RegistersHookForBothContexts()
    {
        // The dedup guard only covers the single-active IDataOwnerProvider; each DbContext must still
        // get its own DataOwnerHook wiring so both applications' SaveChanges pipelines work.
        var services = new ServiceCollection();

        services
            .AddDataOwnerProvider<DddContext, TestDataKeyProvider>()
            .AddDataOwnerProvider<UnrestrictedDddContext, NonEmptyAccessibleKeysProvider>();

        services.Any(s => s.IsKeyedImplementationOf<DataOwnerHook>(typeof(DddContext).FullName!)).ShouldBeTrue();
        services.Any(s => s.IsKeyedImplementationOf<DataOwnerHook>(typeof(UnrestrictedDddContext).FullName!))
            .ShouldBeTrue();
    }

    #endregion
}
