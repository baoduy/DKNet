using Shouldly;
using SlimBus.InterTests.Fixtures;

namespace SlimBus.InterTests.Integration;

public class RateLimitTests(RateLimitFixture api) : IClassFixture<RateLimitFixture>
{
    [Fact]
    public async Task RateLimitCallsProfile()
    {
        var action =
            async () =>
            {
                using var client = await api.CreateHttpClient("Api");
                for (var i = 0; i < 10; i++)
                {
                    var rs = await client.GetAsync("/v1/Profiles?PageIndex=1&PageSize=1");
                    rs.EnsureSuccessStatusCode();
                }
            };

        await action.ShouldThrowAsync<HttpRequestException>();
    }
}