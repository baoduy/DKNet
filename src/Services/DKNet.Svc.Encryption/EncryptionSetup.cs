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
        // Keep AES-CBC registration for backward compatibility while obsolete APIs are phased out.
#pragma warning disable CS0618
        services.AddTransient<IAesEncryption, AesEncryption>();
#pragma warning restore CS0618
        services.AddTransient<IRsaEncryption, RsaEncryption>(_ => new RsaEncryption());
        services.AddTransient<IShaHashing, ShaHashing>();
        services.AddTransient<IHmacHashing, HmacHashing>();

        services.AddTransient<IAesGcmEncryption, AesGcmEncryption>();
        return services;
    }

    #endregion
}
