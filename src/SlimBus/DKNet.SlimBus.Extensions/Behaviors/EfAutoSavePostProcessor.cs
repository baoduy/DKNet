using DKNet.EfCore.Extensions.Extensions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

namespace DKNet.SlimBus.Extensions.Behaviors;

internal sealed class EfAutoSavePostProcessor<TRequest, TResponse>(
    IServiceProvider serviceProvider)
    : IRequestHandlerInterceptor<TRequest, TResponse>, IInterceptorWithOrder
{
    #region Properties

    public int Order => int.MaxValue;

    #endregion

    #region Methods

    public async Task<TResponse> OnHandle(TRequest request, Func<Task<TResponse>> next, IConsumerContext context)
    {
        // Handle the actual request
        var response = await next();

        //If response is null or failed, do not save changes
        if (response is null || response is IResultBase { IsSuccess: false }) return response;

        //If request is a query type, do not save changes
        if (request is Fluents.Queries.IWitResponse<TResponse> ||
            request is Fluents.Queries.IWitPageResponse<TResponse>
            || request is Fluents.EventsConsumers.IHandler<IRequest>) return response;

        // Save changes for all DbContexts with changes
        var dbContexts = serviceProvider.GetServices<DbContext>().Distinct();
        foreach (var db in dbContexts.Where(db => db.ChangeTracker.HasChanges()))
        {
            await db.AddNewEntitiesFromNavigations(context.CancellationToken);
            await db.SaveChangesAsync(context.CancellationToken);
        }

        return response;
    }

    #endregion
}