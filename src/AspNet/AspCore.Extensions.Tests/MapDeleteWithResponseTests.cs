using System.Net.Http.Json;
using AspCore.Extensions.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus.Host;
using SlimMessageBus.Host.Memory;
using SlimMessageBus.Host.Serialization.SystemTextJson;

namespace AspCore.Extensions.Tests;

/// <summary>
///     <c>MapDelete&lt;TCommand, TResponse&gt;</c> (the "with response" DELETE overload) binds its command parameter
///     with no <c>[FromBody]</c>/<c>[AsParameters]</c> — plain <c>(IMessageBus bus, TCommand request)</c>. ASP.NET
///     Core's minimal-API "inferred body" binding does not support DELETE (only POST/PUT/PATCH do), so the endpoint
///     throws <see cref="InvalidOperationException" /> the moment endpoint metadata is built — the first request the
///     host handles at all, not just a request to this route. Kept in its own host (not <see cref="EndpointTestHost" />)
///     because that failure poisons metadata for every other endpoint sharing the same app.
/// </summary>
public class MapDeleteWithResponseTests
{
    #region Methods

    [Fact]
    public async Task MapDelete_WithResponse_DispatchesAndReturnsOk()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSlimMessageBus(mbb => mbb
            .AddJsonSerializer()
            .AddServicesFromAssembly(typeof(MapDeleteWithResponseTests).Assembly)
            .AddChildBus("Memory", mb => mb.WithProviderMemory().AutoDeclareFrom(typeof(MapDeleteWithResponseTests).Assembly)));

        var app = builder.Build();
        app.MapGroup("/t").MapDelete<RenameWidgetCommand, WidgetResult>("/delete-with-response");
        await app.StartAsync();
        using var client = app.GetTestClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Delete, "/t/delete-with-response")
        {
            Content = JsonContent.Create(new RenameWidgetCommand { Name = "renamed" })
        };
        using var response = await client.SendAsync(request);

        // Assert — a DELETE mapped with a response should dispatch through the bus and answer 200 with the
        // handler's response body, the same as every other verb's "with response" overload.
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WidgetResult>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("renamed");

        await app.StopAsync();
    }

    #endregion
}
