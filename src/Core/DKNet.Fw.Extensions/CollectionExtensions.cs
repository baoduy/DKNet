namespace DKNet.Fw.Extensions;

public static class CollectionExtensions
{
    #region Methods

    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
    {
        foreach (var item in items) collection.Add(item);
    }

    #endregion
}