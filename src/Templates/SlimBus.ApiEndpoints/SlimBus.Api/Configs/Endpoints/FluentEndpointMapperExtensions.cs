using SlimBus.Api.Configs.Endpoints;
using SlimBus.Api.Extensions;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Builder;

[ExcludeFromCodeCoverage]
internal static class FluentsEndpointMapperExtensions
{
    public static RouteHandlerBuilder ProducesCommons(this RouteHandlerBuilder routeBuilder) =>
        routeBuilder
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests);

    public static RouteHandlerBuilder MapGet<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Queries.IWitResponse<TResponse>
    {
        return app.MapGet(endpoint,
                async (IMessageBus bus, [AsParameters] TCommand request) =>
                {
                    var rs = await bus.Send(request);
                    return rs is not null ? Results.Ok(rs) : Results.NotFound();
                })
            .Produces<TResponse>()
            .ProducesCommons();
    }

    public static RouteHandlerBuilder MapGetPage<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Queries.IWitPageResponse<TResponse>
    {
        return app.MapGet(endpoint,
                async (IMessageBus bus, [AsParameters] TCommand request) =>
                {
                    var rs = await bus.Send(request);
                    return Results.Ok(new PagedResult<TResponse>(rs));
                })
            .Produces<PagedResult<TResponse>>()
            .ProducesCommons();
    }

    public static RouteHandlerBuilder MapPost<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
    {
        return app.MapPost(endpoint, async (IMessageBus bus, TCommand request) =>
            {
                var rs = await bus.Send(request);
                return rs.Response(true);
            }).Produces<TResponse>()
            .ProducesCommons();
    }

    public static RouteHandlerBuilder MapPost<TCommand>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.INoResponse
    {
        return app.MapPost(endpoint, async (IMessageBus bus, TCommand request) =>
        {
            var rs = await bus.Send(request);
            return rs.Response(true);
        }).ProducesCommons();
    }

    public static RouteHandlerBuilder MapPut<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
    {
        return app.MapPut(endpoint, async (IMessageBus bus, TCommand request) =>
            {
                var rs = await bus.Send(request);
                return rs.Response();
            }).Produces<TResponse>()
            .ProducesCommons();
    }

    public static RouteHandlerBuilder MapPut<TCommand>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.INoResponse
    {
        return app.MapPut(endpoint, async (IMessageBus bus, TCommand request) =>
        {
            var rs = await bus.Send(request);
            return rs.Response();
        }).ProducesCommons();
    }

    public static RouteHandlerBuilder MapPatch<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
    {
        return app.MapPatch(endpoint, async (IMessageBus bus, TCommand request) =>
            {
                var rs = await bus.Send(request);
                return rs.Response();
            }).Produces<TResponse>()
            .ProducesCommons();
    }

    public static RouteHandlerBuilder MapPatch<TCommand>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.INoResponse
    {
        return app.MapPatch(endpoint, async (IMessageBus bus, TCommand request) =>
        {
            var rs = await bus.Send(request);
            return rs.Response();
        }).ProducesCommons();
    }


    public static RouteHandlerBuilder MapDelete<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.IWitResponse<TResponse>
    {
        return app.MapDelete(endpoint, async (IMessageBus bus, TCommand request) =>
            {
                var rs = await bus.Send(request);
                return rs.Response();
            }).Produces<TResponse>()
            .ProducesCommons();
    }

    public static RouteHandlerBuilder MapDelete<TCommand>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.INoResponse
    {
        return app.MapDelete(endpoint, async (IMessageBus bus, [AsParameters] TCommand request) =>
        {
            var rs = await bus.Send(request);
            return rs.Response();
        }).ProducesCommons();
    }
}