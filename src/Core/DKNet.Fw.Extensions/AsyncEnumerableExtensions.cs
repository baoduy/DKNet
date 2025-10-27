// ReSharper disable once CheckNamespace

namespace System.Collections.Generic;

/// <summary>
///     Provides extension methods for <see cref="IAsyncEnumerable{T}" />.
/// </summary>
public static class AsyncEnumerableExtensions
{
    #region Methods

    /// <summary>
    ///     Convert the specified sequence of asynchronous items into a single list.
    /// </summary>
    /// <param name="enumerable">The asynchronous sequence to convert.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    ///     The task result contains an <see cref="IList{T}" /> that contains the elements from the asynchronous sequence.
    /// </returns>
    public static async Task<IList<T>> ToListAsync<T>(this IAsyncEnumerable<T> enumerable)
    {
        var list = new List<T>();
        await foreach (var item in enumerable)
            list.Add(item);
        return list;
    }

    #endregion
}