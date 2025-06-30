using System.Reflection;
using DKNet.EfCore.Extensions.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DKNet.EfCore.Extensions.Registers;

public interface IEntityAutoMappingRegister
{
    /// <summary>
    ///     The Assemblies will be scanned
    /// </summary>
    /// <param name="entityAssemblies"></param>
    /// <returns></returns>
    AutoEntityRegistrationInfo ScanFrom(params Assembly[] entityAssemblies);
}

/// <summary>
///    The Entity Mapping Register
/// </summary>
internal sealed class EntityAutoMappingRegister : IDbContextOptionsExtension, IEntityAutoMappingRegister
{
    private DbContextOptionsExtensionInfo? _info;
    private Action<IServiceCollection>? _extraServiceProvider;

    public DbContextOptionsExtensionInfo Info => _info ??= new EntityMappingExtensionInfo(this);
    internal ICollection<AutoEntityRegistrationInfo> Registrations { get; } = [];

    public void ApplyServices(IServiceCollection services)
    {
        //Add custom services
        _extraServiceProvider?.Invoke(services);

        //Add EntityMappingService using a factory to avoid instance-specific registrations
        services.AddSingleton<EntityMappingRegisterService>(provider => new EntityMappingRegisterService(this));

        //Replace the IModelCustomizer with ExtraModelCustomizer. This only available for Relational Db.
        var originalDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IModelCustomizer));

        if (originalDescriptor == null)
        {
            //it should be
            services.AddScoped<ModelCustomizer, RelationalModelCustomizer>();
            services.Add(new ServiceDescriptor(typeof(IModelCustomizer), typeof(AutoMapModelCustomizer), ServiceLifetime.Scoped));
        }
        else
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            services.Add(new ServiceDescriptor(typeof(ModelCustomizer), originalDescriptor.ImplementationType!,
                originalDescriptor.Lifetime));
            services.Replace(new ServiceDescriptor(typeof(IModelCustomizer), typeof(AutoMapModelCustomizer),
                originalDescriptor.Lifetime));
        }
    }

    /// <summary>
    ///     Register extra service to internal IServiceCollection of EfCore.
    /// </summary>
    /// <param name="provider"></param>
    public void AddExtraServices(Action<IServiceCollection> provider) => _extraServiceProvider = provider;

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

    public void Validate(IDbContextOptions options)
    {
        foreach (var info in Registrations)
            info.Validate();
    }
}