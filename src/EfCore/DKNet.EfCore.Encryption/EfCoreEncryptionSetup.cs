using DKNet.EfCore.Encryption.Encryption;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Encryption;

public static class EfCoreEncryptionSetup
{
    #region Methods

    public static ServiceCollection AddEfCoreEncryption<TKeyServiceImplementation>(this ServiceCollection services)
        where TKeyServiceImplementation : class, IEncryptionKeyProvider
    {
        services.AddSingleton<IEncryptionKeyProvider, TKeyServiceImplementation>();
        return services;
    }

    #endregion
}