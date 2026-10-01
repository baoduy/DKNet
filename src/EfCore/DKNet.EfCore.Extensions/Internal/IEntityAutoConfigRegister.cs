using System.Collections.Frozen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DKNet.EfCore.Extensions.Internal;

/// <summary>
///     The Entity Mapping Register
/// </summary>
/// <param name="assemblies">The assemblies to scan.</param>
/// <param name="globalModelBuilders">The global model builder types captured for this options instance.</param>
internal sealed class EntityAutoConfigRegister(Assembly[] assemblies, IEnumerable<Type> globalModelBuilders)
    : IDbContextOptionsExtension
{
    #region Constructors

    /// <summary>
    ///     Creates the register with a snapshot of the global model builders registered so far
    ///     (<see cref="EfCoreSetup.GlobalModelBuilders" />). Builders registered later do not apply to this instance.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    public EntityAutoConfigRegister(Assembly[] assemblies) : this(assemblies, EfCoreSetup.GlobalModelBuilders)
    {
    }

    #endregion

    #region Fields

    private DbContextOptionsExtensionInfo? _info;

    #endregion

    #region Properties

    public Assembly[] Assemblies { get; } = assemblies;

    /// <summary>
    ///     The distinct global model builder types this options instance is identified by and builds its model with.
    /// </summary>
    public FrozenSet<Type> GlobalModelBuilders { get; } = globalModelBuilders.ToFrozenSet();

    public DbContextOptionsExtensionInfo Info => _info ??= new EntityConfigExtensionInfo(this);

    #endregion

    #region Methods

    public void ApplyServices(IServiceCollection services)
    {
        //Replace the IModelCustomizer with ExtraModelCustomizer. This only available for Relational Db.
        var originalDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IModelCustomizer));

        if (originalDescriptor == null)
        {
            //it should be
            services.AddScoped<ModelCustomizer, RelationalModelCustomizer>();
            services.Add(
                new ServiceDescriptor(
                    typeof(IModelCustomizer),
                    typeof(AutoConfigModelCustomizer),
                    ServiceLifetime.Scoped));
        }
        else
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            services.Add(
                new ServiceDescriptor(
                    typeof(ModelCustomizer),
                    originalDescriptor.ImplementationType!,
                    originalDescriptor.Lifetime));
            services.Replace(
                new ServiceDescriptor(
                    typeof(IModelCustomizer),
                    typeof(AutoConfigModelCustomizer),
                    originalDescriptor.Lifetime));
        }
    }

    public void Validate(IDbContextOptions options)
    {
    }

    #endregion
}