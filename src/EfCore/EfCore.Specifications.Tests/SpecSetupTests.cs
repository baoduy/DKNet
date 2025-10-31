namespace EfCore.Specifications.Tests;

/// <summary>
///     Tests for the SpecSetup dependency injection configuration
/// </summary>
public class SpecSetupTests
{
    #region Methods

    [Fact]
    public void AddSpecRepo_ShouldAllowMultipleRegistrations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options =>
            options.UseSqlServer("Server=localhost;Database=Test;"));

        // Act
        services.AddSpecRepo<TestDbContext>();
        services.AddSpecRepo<TestDbContext>(); // Register twice

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetService<IRepositorySpec>();
        repository.ShouldNotBeNull();
    }

    [Fact]
    public void AddSpecRepo_ShouldRegisterAsScopedService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options =>
            options.UseSqlServer("Server=localhost;Database=Test;"));

        // Act
        services.AddSpecRepo<TestDbContext>();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IRepositorySpec));
        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddSpecRepo_ShouldRegisterRepositorySpec()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options =>
            options.UseSqlServer("Server=localhost;Database=Test;"));

        // Act
        services.AddSpecRepo<TestDbContext>();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var repository = serviceProvider.GetService<IRepositorySpec>();

        repository.ShouldNotBeNull();
        repository.ShouldBeOfType<RepositorySpec<TestDbContext>>();
    }

    [Fact]
    public void AddSpecRepo_ShouldReturnSameServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options =>
            options.UseSqlServer("Server=localhost;Database=Test;"));

        // Act
        var result = services.AddSpecRepo<TestDbContext>();

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddSpecRepo_WithDifferentDbContext_ShouldRegister()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options =>
            options.UseSqlServer("Server=localhost;Database=Test;"));

        // Act
        services.AddSpecRepo<TestDbContext>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        using var scope = serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetService<IRepositorySpec>();
        repository.ShouldNotBeNull();
    }

    #endregion
}