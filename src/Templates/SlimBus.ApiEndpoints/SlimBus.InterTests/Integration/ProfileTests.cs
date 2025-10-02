using System.Net.Http.Json;
using Shouldly;
using SlimBus.AppServices.Profiles.V1.Actions;
using SlimBus.AppServices.Profiles.V1.Events;
using SlimBus.AppServices.Profiles.V1.Queries;
using SlimBus.Infra.Features.Profiles.ExternalEvents;
using SlimBus.InterTests.Extensions;
using SlimBus.InterTests.Fixtures;

namespace SlimBus.InterTests.Integration;

public class ProfileTests(HostFixture api) : IClassFixture<HostFixture>
{
    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    //[InlineData("v3")]
    public async Task CreateProfileMultiVersion(string v)
    {
        ProfileCreatedEventFromMemoryHandler.Called = false;
        ProfileCreatedEmailNotificationHandler.Called = false;

        using var client = await api.CreateHttpClient("Api");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.CreateVersion7().ToString());

        var rp = await client.PostAsJsonAsync($"/{v}/Profiles", new CreateProfileCommand
        {
            Email = $"abc_{v}@hbd.com",
            Name = $"HBD {v}",
            Phone = "+6512345678"
        });

        var (success, result, error, _) = await rp.As<ProfileResult>();

        success.ShouldBeTrue(error?.Detail);
        result.ShouldNotBeNull("ProfileResult should not be null");
        result.Id.ShouldNotBe(Guid.Empty, "Profile ID should not be empty");

        await Task.Delay(TimeSpan.FromSeconds(5));

        ProfileCreatedEventFromMemoryHandler.Called.ShouldBeTrue(
            "ProfileCreatedEventFromMemoryHandler should be called");
    }

    [Fact]
    public async Task CreateDuplicateProfile()
    {
        using var client = await api.CreateHttpClient("Api");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.CreateVersion7().ToString());

        //Create Profile
        await client.PostAsJsonAsync("/v1/Profiles", new CreateProfileCommand
        {
            Email = "abc1@hbd.com",
            Name = "HBD",
            Phone = "+6512345678"
        });

        //And create other with the same email
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.CreateVersion7().ToString());
        var rp = await client.PostAsJsonAsync("/v1/Profiles", new CreateProfileCommand
        {
            Email = "abc1@hbd.com",
            Name = "HBD",
            Phone = "+6512345678"
        });
        var (success, _, error, _) = await rp.As<ProfileResult>();

        success.ShouldBeFalse();
        error.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateProfile()
    {
        using var client = await api.CreateHttpClient("Api");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.CreateVersion7().ToString());
        //Create Profile
        var created = await client.PostAsJsonAsync("/v1/Profiles", new CreateProfileCommand
        {
            Email = "update_test@hbd.com",
            Name = "Duy Hoang",
            Phone = "+6512345678"
        });

        var (_, createdResult, createdError, _) = await created.As<ProfileResult>();
        createdResult.ShouldNotBeNull(createdError?.Detail);

        //Update
        var rp = await client.PutAsJsonAsync($"/v1/Profiles/{createdResult.Id}", new UpdateProfileCommand
        {
            Id = createdResult.Id,
            Name = "HBD New",
            Phone = "+6512399999"
        });

        var (success, result, error, _) = await rp.As<ProfileResult>();

        success.ShouldBeTrue(error?.Detail);
        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteProfile()
    {
        using var client = await api.CreateHttpClient("Api");
        client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.CreateVersion7().ToString());

        //Create Profile
        var created = await client.PostAsJsonAsync("/v1/Profiles", new CreateProfileCommand
        {
            Email = "delete_test@hbd.com",
            Name = "Steven Hoang",
            Phone = "+6512345678"
        });

        var (_, createdResult, createdError, _) = await created.As<ProfileResult>();
        createdResult.ShouldNotBeNull(createdError?.Detail);

        //Delete
        var rp = await client.DeleteAsync($"/v1/Profiles/{createdResult.Id}");

        var (success, _, error, _) = await rp.As<ProfileResult>();
        success.ShouldBeTrue(error?.Detail);
    }
}