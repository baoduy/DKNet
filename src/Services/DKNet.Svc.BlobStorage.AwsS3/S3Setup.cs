using DKNet.Svc.BlobStorage.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.Svc.BlobStorage.AwsS3;

public static class S3Setup
{
    public static IServiceCollection AddS3BlobService(this IServiceCollection services, IConfiguration configuration)
    {
        return services
            .Configure<S3Options>(o => configuration.GetSection(S3Options.Name).Bind(o))
            .AddScoped<IBlobService, S3BlobService>();
    }
}