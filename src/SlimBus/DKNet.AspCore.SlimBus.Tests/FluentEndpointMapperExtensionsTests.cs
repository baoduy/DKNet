using DKNet.SlimBus.Extensions;
using FluentResults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SlimMessageBus;
using X.PagedList;
using static DKNet.SlimBus.Extensions.Fluents;

namespace DKNet.AspCore.SlimBus.Tests;

public class FluentEndpointMapperExtensionsTests
{
    #region Test Request/Response Types

    public record TestCommand : Requests.IWitResponse<TestResponse>
    {
        public string Value { get; init; } = string.Empty;
    }

    public record TestCommandNoResponse : Requests.INoResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    public record TestQuery : Queries.IWitResponse<TestResponse>
    {
        public string Value { get; init; } = string.Empty;
    }

    public record TestPageQuery : Queries.IWitPageResponse<TestResponse>
    {
        public string Value { get; init; } = string.Empty;
    }

    public record TestResponse
    {
        public string Result { get; init; } = string.Empty;
    }

    #endregion

    #region Helper Methods

    private static RouteGroupBuilder CreateRouteGroupBuilder()
    {
        var services = new ServiceCollection();
        var app = WebApplication.CreateBuilder().Build();
        return app.MapGroup("/test");
    }

    #endregion

    #region Tests

    [Fact]
    public void MapDelete_WithResponse_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();

        // Act
        var builder = group.MapDelete<TestCommand, TestResponse>("/endpoint");

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RouteHandlerBuilder>();
    }

    [Fact]
    public void MapDelete_NoResponse_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();

        // Act
        var builder = group.MapDelete<TestCommandNoResponse>("/endpoint");

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RouteHandlerBuilder>();
    }

    [Fact]
    public void MapGet_WithResponse_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();

        // Act
        var builder = group.MapGet<TestQuery, TestResponse>("/endpoint");

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RouteHandlerBuilder>();
    }

    [Fact]
    public void MapGetPage_WithResponse_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();

        // Act
        var builder = group.MapGetPage<TestPageQuery, TestResponse>("/endpoint");

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RouteHandlerBuilder>();
    }

    [Fact]
    public void MapPatch_WithResponse_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();

        // Act
        var builder = group.MapPatch<TestCommand, TestResponse>("/endpoint");

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RouteHandlerBuilder>();
    }

    [Fact]
    public void MapPatch_NoResponse_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();

        // Act
        var builder = group.MapPatch<TestCommandNoResponse>("/endpoint");

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RouteHandlerBuilder>();
    }

    [Fact]
    public void MapPost_WithResponse_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();

        // Act
        var builder = group.MapPost<TestCommand, TestResponse>("/endpoint");

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RouteHandlerBuilder>();
    }

    [Fact]
    public void MapPost_NoResponse_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();

        // Act
        var builder = group.MapPost<TestCommandNoResponse>("/endpoint");

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RouteHandlerBuilder>();
    }

    [Fact]
    public void MapPut_WithResponse_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();

        // Act
        var builder = group.MapPut<TestCommand, TestResponse>("/endpoint");

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RouteHandlerBuilder>();
    }

    [Fact]
    public void MapPut_NoResponse_ReturnsRouteHandlerBuilder()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();

        // Act
        var builder = group.MapPut<TestCommandNoResponse>("/endpoint");

        // Assert
        builder.ShouldNotBeNull();
        builder.ShouldBeOfType<RouteHandlerBuilder>();
    }

    [Fact]
    public void ProducesCommons_AddsCommonStatusCodes()
    {
        // Arrange
        var group = CreateRouteGroupBuilder();
        var builder = group.MapGet("/test", () => "test");

        // Act
        var result = builder.ProducesCommons();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<RouteHandlerBuilder>();
    }

    #endregion
}
