using SlimBus.App.Tests.Fixtures;

namespace SlimBus.App.Tests.Integration;

public class RateLimitTests(RateLimitFixture api) : IClassFixture<RateLimitFixture>
{
    [Fact]
    public async Task RateLimitCallsProfile()
    {
        var client = api.CreateClient();

        var action =
            async () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    var rs = await client.GetAsync("/v1/Profiles?PageIndex=1&PageSize=1");
                    rs.EnsureSuccessStatusCode();
                }
            };

        await action.ShouldThrowAsync<HttpRequestException>();
    }
}