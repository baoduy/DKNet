using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DKNet.EfCore.Extensions.Internal;

/// <summary>
///     The Entity Mapping Register
/// </summary>
internal sealed class EntityAutoConfigRegister : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;
    public DbContextOptionsExtensionInfo Info => _info ??= new EntityConfigExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
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
    }
}