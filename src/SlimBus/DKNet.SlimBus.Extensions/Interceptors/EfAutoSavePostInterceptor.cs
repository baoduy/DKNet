using DKNet.EfCore.Extensions.Extensions;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SlimMessageBus;
using SlimMessageBus.Host.Interceptor;

namespace DKNet.SlimBus.Extensions.Interceptors;

internal sealed class EfAutoSavePostInterceptor<TRequest, TResponse>(
    IServiceProvider serviceProvider,
    ILogger<EfAutoSavePostInterceptor<TRequest, TResponse>> logger)
    : IRequestHandlerInterceptor<TRequest, TResponse>, IInterceptorWithOrder
{
    #region Properties

    public int Order => int.MaxValue;

    #endregion

    #region Methods

    public async Task<TResponse> OnHandle(TRequest request, Func<Task<TResponse>> next, IConsumerContext context)
    {
        var requestTypeName = typeof(TRequest).Name;
        logger.LogDebug("Handling request of type {RequestType}", requestTypeName);

        var response = await next().ConfigureAwait(false);

        if (response is null)
        {
            logger.LogDebug("Response is null. Skipping auto-save.");
            return response;
        }

        if (response is IResultBase { IsSuccess: false })
        {
            logger.LogDebug("Response indicates failure. Skipping auto-save.");
            return response;
        }

        if (request is Fluents.Queries.IWitResponse<TResponse> ||
            request is Fluents.Queries.IWitPageResponse<TResponse> ||
            request is Fluents.EventsConsumers.IHandler<IRequest>)
        {
            logger.LogDebug("Request is a query or event handler. Skipping auto-save.");
            return response;
        }

        var exceptionHandler = serviceProvider.GetService(typeof(IEfCoreExceptionHandler)) as IEfCoreExceptionHandler;
        var dbContexts = EfAutoSavePostProcessorRegistration.DbContextTypes
            .Select(serviceProvider.GetService).OfType<DbContext>().ToArray();

        var dbContextCount = dbContexts.Length;
        logger.LogDebug("Found {DbContextCount} DbContext(s) for auto-save.", dbContextCount);

        foreach (var db in dbContexts.Where(db => db.ChangeTracker.HasChanges()))
        {
            var dbContextTypeName = db.GetType().Name;
            logger.LogDebug("DbContext {DbContextType} has changes. Saving...", dbContextTypeName);

            await db.AddNewEntitiesFromNavigations(context.CancellationToken).ConfigureAwait(false);
            await db.SaveChangesWithConcurrencyHandlingAsync(exceptionHandler, context.CancellationToken)
                .ConfigureAwait(false);

            logger.LogDebug("DbContext {DbContextType} changes saved.", dbContextTypeName);
        }

        logger.LogDebug("Auto-save post-processing complete for request {RequestType}.", requestTypeName);

        return response;
    }

    #endregion
}