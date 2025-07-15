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
    public int Order
    {
        get => int.MaxValue;
    }

    public async Task<TResponse> OnHandle(TRequest request, Func<Task<TResponse>> next, IConsumerContext context)
    {
        // Handle the actual request
        var response = await next();

        if (response is null || request is Fluents.Queries.IWitResponse<TResponse> ||
            request is Fluents.Queries.IWitPageResponse<TResponse>) return response;
        if (response is IResultBase { IsSuccess: false }) return response;

        var dbContexts = serviceProvider.GetServices<DbContext>().ToHashSet();
        foreach (var db in dbContexts.Where(db => db.ChangeTracker.HasChanges()))
        {
            await db.AddNewEntitiesFromNavigations();
            await db.SaveChangesAsync(context.CancellationToken);
        }

        return response;
    }
}