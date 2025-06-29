using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Hooks.Internals;

/// <summary>
/// EF Core extension that registers hook services in EF Core's internal service provider.
/// This avoids the service provider proliferation issue.
/// </summary>
internal sealed class HookOptionsExtension(IServiceProvider applicationServiceProvider) : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public DbContextOptionsExtensionInfo Info => _info ??= new HookOptionsExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
        // Register the hook runner as a singleton in EF Core's internal container
        // This avoids creating multiple instances and service provider proliferation
        services.AddSingleton<HookRunner>(provider => 
        {
            // Create the hook runner with a reference to the application service provider
            // but don't resolve anything from it until actually needed
            return new HookRunner(applicationServiceProvider);
        });

        // Register the interceptor
        services.AddSingleton<ISaveChangesInterceptor>(provider => 
            provider.GetRequiredService<HookRunner>());
    }

    public void Validate(IDbContextOptions options)
    {
        // No validation needed
    }

    /// <summary>
    /// Extension info for the hook options extension
    /// </summary>
    private sealed class HookOptionsExtensionInfo(IDbContextOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => false;

        public override string LogFragment => "HooksEnabled";

        public override int GetServiceProviderHashCode()
        {
            // Use a constant hash code since we want the same service provider
            // to be reused for all contexts of the same type
            return typeof(HookOptionsExtension).GetHashCode();
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
        {
            // Always use the same service provider for hook extensions
            return other is HookOptionsExtensionInfo;
        }

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            debugInfo["Hooks"] = "Enabled";
        }
    }
}