using DKNet.Svc.Encryption.Ciphers;
using DKNet.Svc.Encryption.Hashing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DKNet.Svc.Encryption;

/// <summary>
///     Provides EncryptionSetup functionality.
/// </summary>
public static class EncryptionSetup
{
    #region Methods

    /// <summary>
    ///     Registers the keyless hashing services (<see cref="IShaHashing" />, <see cref="IHmacHashing" />). For the
    ///     key-taking ciphers, call <see cref="AddAesGcmEncryption" /> or <see cref="AddRsaEncryption" /> with an
    ///     explicit key so resolutions stay key-stable.
    /// </summary>
    public static IServiceCollection AddEncryptionServices(this IServiceCollection services)
    {
        services.TryAddTransient<IShaHashing, ShaHashing>();
        services.TryAddTransient<IHmacHashing, HmacHashing>();

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

    /// <summary>
    ///     Registers <see cref="IAesGcmEncryption" /> as a singleton constructed from the supplied Base64 encoded
    ///     AES-GCM key. The same instance (and therefore the same key) is returned for every resolution, so data
    ///     encrypted via one resolution can be decrypted via another.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="base64Key">
    ///     The Base64 encoded 128/192/256-bit AES-GCM key. Source this from configuration or a key vault — never
    ///     hardcode it.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="base64Key" /> is null, empty, or whitespace.
    /// </exception>
    public static IServiceCollection AddAesGcmEncryption(this IServiceCollection services, string base64Key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Key);

        if (!services.Any(s => s.ServiceType == typeof(IAesGcmEncryption)))
            services.AddSingleton<IAesGcmEncryption>(_ => new AesGcmEncryption(base64Key));

        return services;
    }

    #endregion
}
