using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using DKNet.EfCore.Repos.Repositories;
using DKNet.EfCore.Specifications.Repositories;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Repos.Tests.Repositories;

/// <summary>
///     Pins the <c>Query&lt;TModel&gt;</c> virtual-dispatch fix (DRK-328 / DRK-354, commit 32e80fe).
///     Before the fix, <see cref="ReadRepository{TEntity}" />.Query&lt;TModel&gt; was non-virtual and
///     <see cref="Repository{TEntity}" /> hid it with <c>new</c> instead of overriding it. A call to
///     <c>Query&lt;TModel&gt;</c> made through a reference statically typed as the base
///     <see cref="ReadRepository{TEntity}" /> class — which is exactly how <see cref="IReadRepository{TEntity}" />'s
///     read surface is implemented — bound at compile time to the hidden base implementation, whose own
///     <c>IMapper</c> field was never populated (only <see cref="Repository{TEntity}" />'s own field was), so the
///     call threw <see cref="InvalidOperationException" /> even though the concrete <see cref="Repository{TEntity}" />
///     instance had a valid mapper. Making the base method <c>virtual</c> and the override <c>override</c> lets the
///     call dispatch to <see cref="Repository{TEntity}" />'s implementation regardless of the static reference type.
/// </summary>
public class RepositoryQueryModelDispatchTests(RepositoryFixture fixture) : IClassFixture<RepositoryFixture>
{
    #region Methods

    private static IMapper CreateMapper()
    {
        var config = new TypeAdapterConfig();
        config.NewConfig<User, UserDto>()
            .Map(dest => dest.FirstName, src => src.FirstName)
            .Map(dest => dest.LastName, src => src.LastName);
        return new Mapper(config);
    }

    private ReadRepository<User> CreateRepositoryWithMapperAsBaseReference()
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateMapper());
        var provider = services.BuildServiceProvider();

        // Repository<User> is constructed with an IMapper registered in the provider, then handed back
        // through a ReadRepository<User> reference — the same shape IReadRepository<TEntity>'s inherited
        // read surface dispatches through.
        return new Repository<User>(fixture.DbContext!, provider);
    }

    [Fact]
    public async Task QueryOfTModelViaBaseReferenceDoesNotThrowWhenMapperIsRegistered()
    {
        // Arrange
        ReadRepository<User> repository = CreateRepositoryWithMapperAsBaseReference();

        // Act
        var projection = repository.Query<UserDto>(u => u.FirstName == "DispatchProbe");

        // Assert
        var result = await projection.ToListAsync();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task QueryOfTModelViaBaseReferenceProjectsMatchingDtoFields()
    {
        // Arrange
        fixture.DbContext!.ChangeTracker.Clear();
        var entity = new User("dispatchprobe1") { FirstName = "DispatchProbe", LastName = "Test" };
        fixture.DbContext.Add(entity);
        await fixture.DbContext.SaveChangesAsync();

        ReadRepository<User> repository = CreateRepositoryWithMapperAsBaseReference();

        // Act
        var result = await repository.Query<UserDto>(u => u.FirstName == "DispatchProbe").FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entity.FirstName, result.FirstName);
        Assert.Equal(entity.LastName, result.LastName);
    }

    #endregion
}
