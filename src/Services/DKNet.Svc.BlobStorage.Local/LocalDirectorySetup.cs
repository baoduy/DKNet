using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.Local;
using Microsoft.Extensions.Configuration;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class LocalDirectorySetup
{
    public static bool IsDirectory(this string path)
    {
        try
        {
            var attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Directory);
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static IServiceCollection AddLocalDirectoryBlobService(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .Configure<LocalDirectoryOptions>(o => configuration.GetSection(LocalDirectoryOptions.Name).Bind(o))
            .AddScoped<IBlobService, LocalBlobService>();
    }
}