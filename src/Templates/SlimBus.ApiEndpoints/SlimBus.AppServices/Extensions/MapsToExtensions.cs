using System.Reflection;
using DKNet.Fw.Extensions.TypeExtractors;

namespace SlimBus.AppServices.Extensions;

internal static class MapsToExtensions
{
    #region Methods

    public static void ScanMapsTo(this TypeAdapterConfig config)
    {
        var mapsToTypes = typeof(MapsToExtensions).Assembly
            .Extract().Classes().NotAbstract().NotGeneric().HasAttribute<MapsToAttribute>();

        foreach (var type in mapsToTypes)
        {
            var attribute = type.GetCustomAttribute<MapsToAttribute>();
            if (attribute == null)
            {
                continue;
            }

            var ctor = attribute.EntityType.GetConstructors().First(c => c.IsPublic);
            config.NewConfig(type, attribute.EntityType)

                //.PreserveReference(true)
                .Settings.MapToConstructor = ctor;
        }
    }

    #endregion
}