using Microsoft.EntityFrameworkCore;

namespace DKNet.SlimBus.Extensions.Interceptors;

/// <summary>
/// Provides extension methods for <see cref="DbContext"/> to handle EF Core concurrency exceptions
/// during save operations with configurable retry and resolution behavior.
/// </summary>
public static class EfSaveChangesExtension
{
    #region Methods

    /// <summary>
    /// Attempts to save changes on the provided <see cref="DbContext"/>, handling <see cref="DbUpdateConcurrencyException"/>
    /// according to the supplied <see cref="IEfCoreExceptionHandler"/>. The method will retry the save operation
    /// if the handler resolves the concurrency conflict with <see cref="EfConcurrencyResolution.RetrySaveChanges"/>.
    /// </summary>
    /// <param name="dbContext">The <see cref="DbContext"/> to save.</param>
    /// <param name="handler">
    /// An optional concurrency exception handler that decides how to resolve <see cref="DbUpdateConcurrencyException"/>.
    /// If <c>null</c>, a default <see cref="EfCoreExceptionHandler"/> instance will be used.
    /// </param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A <see cref="Task{Int32}"/> representing the asynchronous operation. The task result contains the number
    /// of state entries written to the underlying database. If the handler resolves the conflict with
    /// <see cref="EfConcurrencyResolution.IgnoreChanges"/>, <c>0</c> is returned.
    /// </returns>
    /// <remarks>
    /// The method will loop and retry calls to <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>
    /// when the handler returns <see cref="EfConcurrencyResolution.RetrySaveChanges"/>. The number of retries
    /// is limited by <see cref="IEfCoreExceptionHandler.MaxRetryCount"/> on the provided handler. If the retry
    /// count exceeds <see cref="IEfCoreExceptionHandler.MaxRetryCount"/>, the method returns <c>0</c>.
    /// </remarks>
    /// <exception cref="DbUpdateConcurrencyException">
    /// Thrown when a concurrency exception occurs and the handler returns
    /// <see cref="EfConcurrencyResolution.RethrowException"/>.
    /// </exception>
    public static async Task<int> SaveChangesWithConcurrencyHandlingAsync(
        this DbContext dbContext,
        IEfCoreExceptionHandler? handler = null,
        CancellationToken cancellationToken = default)
    {
        handler ??= new EfCoreExceptionHandler();
        var retryCount = 0;

        while (true)
            try
            {
                return await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var rs = await handler.HandlingAsync(dbContext, ex, cancellationToken).ConfigureAwait(false);
                switch (rs)
                {
                    case EfConcurrencyResolution.RethrowException:
                        throw;
                    case EfConcurrencyResolution.IgnoreChanges:
                        return 0;
                    case EfConcurrencyResolution.RetrySaveChanges:
                        retryCount += 1;
                        if (retryCount > handler.MaxRetryCount) return 0;
                        break;
                }
            }
    }

    #endregion
}
