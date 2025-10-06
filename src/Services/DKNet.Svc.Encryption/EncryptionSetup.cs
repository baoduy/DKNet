using Microsoft.Extensions.DependencyInjection;

namespace DKNet.Svc.Encryption;

public static class EncryptionSetup
{
    public static IServiceCollection AddEncryptionServices(this IServiceCollection services)
    {
        services.AddTransient<IAesEncryption, AesEncryption>();
        services.AddTransient<IRsaEncryption, RsaEncryption>(_ => new RsaEncryption());
        services.AddTransient<IShaHashing, ShaHashing>();
        services.AddTransient<IHmacHashing, HmacHashing>();
        //services.AddTransient<IPasswordAesEncryption, PasswordAesEncryption>();
        services.AddTransient<IAesGcmEncryption, AesGcmEncryption>();
        return services;
    }
}