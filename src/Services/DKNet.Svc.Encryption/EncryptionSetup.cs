using Microsoft.Extensions.DependencyInjection;

namespace DKNet.Svc.Encryption;

/// <summary>
///     Provides EncryptionSetup functionality.
/// </summary>
public static class EncryptionSetup
{
    #region Methods

    /// <summary>
    ///     AddEncryptionServices operation.
    /// </summary>
    public static IServiceCollection AddEncryptionServices(this IServiceCollection services)
    {
        // AES-CBC removed: vulnerable to padding oracle attacks. Use IAesGcmEncryption.
        // services.AddTransient<IAesEncryption, AesEncryption>();
        services.AddTransient<IRsaEncryption, RsaEncryption>(_ => new RsaEncryption());
        services.AddTransient<IShaHashing, ShaHashing>();
        services.AddTransient<IHmacHashing, HmacHashing>();

        services.AddTransient<IAesGcmEncryption, AesGcmEncryption>();
        return services;
    }

    #endregion
}