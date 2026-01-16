using Microsoft.EntityFrameworkCore;

namespace DKNet.SlimBus.Extensions.Interceptors;

public static class EfSaveChangesExtension
{
    #region Methods

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
