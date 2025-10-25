using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DKNet.EfCore.Specifications;

public static class SpecSetup
{
    public static IServiceCollection AddSpecRepo<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext =>
        services.AddScoped<IRepositorySpec, RepositorySpec<TDbContext>>();
}