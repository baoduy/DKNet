using DKNet.EfCore.Extensions.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DKNet.EfCore.Extensions.Registers;

public interface IEntityAutoConfigRegister
{
    /// <summary>
    ///     The Assemblies will be scanned
    /// </summary>
    /// <param name="entityAssemblies"></param>
    /// <returns></returns>
    AutoEntityRegistrationInfo ScanFrom(params Assembly[] entityAssemblies);
}

/// <summary>
///     The Entity Mapping Register
/// </summary>
internal sealed class EntityAutoConfigRegister : IDbContextOptionsExtension, IEntityAutoConfigRegister
{
    private Action<IServiceCollection>? _extraServiceProvider;
    private DbContextOptionsExtensionInfo? _info;
    internal ICollection<AutoEntityRegistrationInfo> Registrations { get; } = [];

    public DbContextOptionsExtensionInfo Info
    {
        get => _info ??= new EntityConfigExtensionInfo(this);
    }

    public void ApplyServices(IServiceCollection services)
    {
        //Add custom services
        _extraServiceProvider?.Invoke(services);

        //Add EntityMappingService using a factory to avoid instance-specific registrations
        services.AddSingleton<EntityConfigRegisterService>(provider => new EntityConfigRegisterService(this));

        //Replace the IModelCustomizer with ExtraModelCustomizer. This only available for Relational Db.
        var originalDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IModelCustomizer));

        if (originalDescriptor == null)
        {
            //it should be
            services.AddScoped<ModelCustomizer, RelationalModelCustomizer>();
            services.Add(new ServiceDescriptor(typeof(IModelCustomizer), typeof(AutoConfigModelCustomizer),
                ServiceLifetime.Scoped));
        }
        else
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            services.Add(new ServiceDescriptor(typeof(ModelCustomizer), originalDescriptor.ImplementationType!,
                originalDescriptor.Lifetime));
            services.Replace(new ServiceDescriptor(typeof(IModelCustomizer), typeof(AutoConfigModelCustomizer),
                originalDescriptor.Lifetime));
        }
    }

    public void Validate(IDbContextOptions options)
    {
        foreach (var info in Registrations)
            info.Validate();
    }

    /// <inheritdoc />
    /// <summary>
    ///     The Assemblies will be scanned.
    ///     The IGlobalQueryFilterRegister also will be scanned from these assemblies and load into internal ServiceProvider.
    ///     If you have any IGlobalQueryFilterRegister implementation. Just provider the assemblies here and no need to add
    ///     them to any other DI.
    /// </summary>
    /// <param name="entityAssemblies"></param>
    /// <returns></returns>
    public AutoEntityRegistrationInfo ScanFrom(params Assembly[] entityAssemblies)
    {
        if (entityAssemblies.Length == 0)
            entityAssemblies = [Assembly.GetCallingAssembly()];

        var register = new AutoEntityRegistrationInfo(entityAssemblies);
        Registrations.Add(register);
        return register;
    }

    /// <summary>
    ///     Register extra service to internal IServiceCollection of EfCore.
    /// </summary>
    /// <param name="provider"></param>
    public void AddExtraServices(Action<IServiceCollection> provider)
    {
        _extraServiceProvider = provider;
    }
}