using DKNet.Svc.Encryption.Ciphers;
using DKNet.Svc.Encryption.Hashing;
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
        if (!services.Any(s => s.ServiceType == typeof(IAesEncryption)))
            services.AddTransient<IAesEncryption, AesEncryption>();
#pragma warning restore CS0618

        if (!services.Any(s => s.ServiceType == typeof(IShaHashing)))
            services.AddTransient<IShaHashing, ShaHashing>();

        if (!services.Any(s => s.ServiceType == typeof(IHmacHashing)))
            services.AddTransient<IHmacHashing, HmacHashing>();

        if (!services.Any(s => s.ServiceType == typeof(IAesGcmEncryption)))
            services.AddTransient<IAesGcmEncryption, AesGcmEncryption>();

        return services;
    }

    /// <summary>
    ///     Registers <see cref="IRsaEncryption" /> as a singleton constructed from the supplied Base64 encoded
    ///     PKCS#1 private key. The same instance (and therefore the same key) is returned for every resolution,
    ///     so data encrypted/signed via one resolution can be decrypted/verified via another.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="privateKeyBase64">
    ///     The Base64 encoded PKCS#1 private key. Source this from configuration or a key vault — never hardcode it.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="privateKeyBase64" /> is null, empty, or whitespace.
    /// </exception>
    public static IServiceCollection AddRsaEncryption(this IServiceCollection services, string privateKeyBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyBase64);

        if (!services.Any(s => s.ServiceType == typeof(IRsaEncryption)))
            services.AddSingleton<IRsaEncryption>(_ => new RsaEncryption(privateKeyBase64));

        return services;
    }

    #endregion
}
