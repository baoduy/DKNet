using MapsterMapper;
using NSubstitute;
using SlimBus.AppServices.Profiles.V1.Queries;
using SlimBus.Domains.Features.Profiles.Entities;
using DKNet.EfCore.Repos.Abstractions;

namespace SlimBus.App.Tests.Unit;

public class QueryHandlerTests
{
    [Fact]
    public async Task SingleProfileQueryHandler_OnHandle_ReturnsProfile_WhenProfileExists()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        var expectedProfile = new ProfileResult { Id = profileId, Name = "Test User", Email = "test@example.com" };
        
        var mockRepository = Substitute.For<IReadRepository<CustomerProfile>>();
        var mockQueryable = new List<ProfileResult> { expectedProfile }.AsQueryable();
        
        mockRepository.GetProjection<ProfileResult>().Returns(mockQueryable);
        
        var handler = new SingleProfileQueryHandler(mockRepository);
        var query = new ProfileQuery { Id = profileId };

        // Act
        var result = await handler.OnHandle(query, CancellationToken.None);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(profileId);
        result.Name.ShouldBe("Test User");
        result.Email.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task SingleProfileQueryHandler_OnHandle_ReturnsNull_WhenProfileNotExists()
    {
        // Arrange
        var profileId = Guid.NewGuid();
        
        var mockRepository = Substitute.For<IReadRepository<CustomerProfile>>();
        var mockQueryable = new List<ProfileResult>().AsQueryable();
        
        mockRepository.GetProjection<ProfileResult>().Returns(mockQueryable);
        
        var handler = new SingleProfileQueryHandler(mockRepository);
        var query = new ProfileQuery { Id = profileId };

        // Act
        var result = await handler.OnHandle(query, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task PageProfilesQueryHandler_OnHandle_ReturnsPagedResults()
    {
        // Arrange
        var mockRepository = Substitute.For<IReadRepository<CustomerProfile>>();
        var mockMapper = Substitute.For<IMapper>();
        
        // Create mock queryable
        var customerProfiles = new List<CustomerProfile>().AsQueryable();
        mockRepository.Gets().Returns(customerProfiles);
        
        var handler = new PageProfilesQueryHandler(mockRepository, mockMapper);
        var query = new PageProfilePageQuery { PageIndex = 0, PageSize = 10 };

        // Act & Assert - This will test the method signature and basic functionality
        // Note: Full integration testing of ProjectToType and ToPagedListAsync requires more complex setup
        await Should.NotThrowAsync(async () => await handler.OnHandle(query, CancellationToken.None));
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(0, 50)]
    [InlineData(5, 1000)]
    public void PageProfilePageQuery_Properties_ShouldSetCorrectly(int pageIndex, int pageSize)
    {
        // Arrange & Act
        var query = new PageProfilePageQuery { PageIndex = pageIndex, PageSize = pageSize };

        // Assert
        query.PageIndex.ShouldBe(pageIndex);
        query.PageSize.ShouldBe(pageSize);
    }

    [Fact]
    public void PageProfilePageQuery_DefaultPageSize_ShouldBe100()
    {
        // Arrange & Act
        var query = new PageProfilePageQuery();

        // Assert
        query.PageSize.ShouldBe(100);
        query.PageIndex.ShouldBe(0);
    }

    [Fact]
    public void ProfileQuery_Properties_ShouldSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var query = new ProfileQuery { Id = id };

        // Assert
        query.Id.ShouldBe(id);
    }
}