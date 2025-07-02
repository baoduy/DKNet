using SlimBus.Api.Extensions;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Builder;

[ExcludeFromCodeCoverage]
internal static class FluentsEndpointMapperExtensions
{
    public static RouteHandlerBuilder ProducesCommons(this RouteHandlerBuilder routeBuilder)
        => routeBuilder
            .Produces<ProblemDetails>(statusCode: StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(statusCode: StatusCodes.Status500InternalServerError)
            .Produces(statusCode: StatusCodes.Status401Unauthorized)
            .Produces(statusCode: StatusCodes.Status403Forbidden)
            .Produces(statusCode: StatusCodes.Status404NotFound)
            .Produces(statusCode:StatusCodes.Status409Conflict)
            .Produces(statusCode:StatusCodes.Status429TooManyRequests);

    public static RouteHandlerBuilder MapGet<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Queries.IWitResponse<TResponse> =>
        app.MapGet(endpoint,
                async (IMessageBus bus, [AsParameters] TCommand request) =>
                {
                    var rs = await bus.Send(request);
                    return rs is not null ? Results.Ok(rs) : Results.NotFound();
                })
            .Produces<TResponse>()
            .ProducesCommons();

    public static RouteHandlerBuilder MapGetPage<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Queries.IWitPageResponse<TResponse> =>
        app.MapGet(endpoint,
                async (IMessageBus bus, [AsParameters] TCommand request) => Results.Ok(await bus.Send(request)))
            .Produces<TResponse>()
            .ProducesCommons();

    public static RouteHandlerBuilder MapPost<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.IWitResponse<TResponse> =>
        app.MapPost(endpoint, async (IMessageBus bus, TCommand request) =>
            {
                var rs = await bus.Send(request);
                return rs.Response(isCreated: true);
            }).Produces<TResponse>()
            .ProducesCommons();

    public static RouteHandlerBuilder MapPost<TCommand>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.INoResponse =>
        app.MapPost(endpoint, async (IMessageBus bus, TCommand request) =>
        {
            var rs = await bus.Send(request);
            return rs.Response(isCreated: true);
        }).ProducesCommons();

    public static RouteHandlerBuilder MapPut<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.IWitResponse<TResponse> =>
        app.MapPut(endpoint, async (IMessageBus bus, TCommand request) =>
            {
                var rs = await bus.Send(request);
                return rs.Response();
            }).Produces<TResponse>()
            .ProducesCommons();

    public static RouteHandlerBuilder MapPut<TCommand>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.INoResponse =>
        app.MapPut(endpoint, async (IMessageBus bus, TCommand request) =>
        {
            var rs = await bus.Send(request);
            return rs.Response();
        }).ProducesCommons();

    public static RouteHandlerBuilder MapPatch<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.IWitResponse<TResponse> =>
        app.MapPatch(endpoint, async (IMessageBus bus, TCommand request) =>
            {
                var rs = await bus.Send(request);
                return rs.Response();
            }).Produces<TResponse>()
            .ProducesCommons();

    public static RouteHandlerBuilder MapPatch<TCommand>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.INoResponse =>
        app.MapPatch(endpoint, async (IMessageBus bus, TCommand request) =>
        {
            var rs = await bus.Send(request);
            return rs.Response();
        }).ProducesCommons();


    public static RouteHandlerBuilder MapDelete<TCommand, TResponse>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.IWitResponse<TResponse> =>
        app.MapDelete(endpoint, async (IMessageBus bus, TCommand request) =>
            {
                var rs = await bus.Send(request);
                return rs.Response();
            }).Produces<TResponse>()
            .ProducesCommons();

    public static RouteHandlerBuilder MapDelete<TCommand>(this RouteGroupBuilder app, string endpoint)
        where TCommand : class, Fluents.Requests.INoResponse =>
        app.MapDelete(endpoint, async (IMessageBus bus, [AsParameters] TCommand request) =>
        {
            var rs = await bus.Send(request);
            return rs.Response();
        }).ProducesCommons();
}