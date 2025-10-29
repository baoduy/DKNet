using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace DKNet.Svc.Transformation.TokenExtractors;

/// <summary>
///     Interface for TokenResolver operations.
/// </summary>
public interface ITokenResolver
{
    #region Methods

    /// <summary>
    ///     Get value from data based on toke <see cref="IToken" />
    /// </summary>
    /// <param name="token">
    ///     <see cref="IToken" />
    /// </param>
    /// <param name="data"> data object or IDictionary[string, object]</param>
    /// <returns>value found from data or NULL</returns>
    object? Resolve(IToken token, params object?[] data);

    /// <summary>
    ///     Get value from data based on toke <see cref="IToken" />
    /// </summary>
    /// <param name="token">
    ///     <see cref="IToken" />
    /// </param>
    /// <param name="data"> data object or IDictionary[string, object]</param>
    /// <returns>value found from data or NULL</returns>
    Task<object?> ResolveAsync(IToken token, params object?[] data);

    #endregion
}

internal sealed class TokenResolver : ITokenResolver
{
    #region Methods

    /// <summary>
    ///     Get the first not null value of the public property of data.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="data"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">if data or token is null</exception>
    public object? Resolve(IToken token, params object?[] data)
    {
        ArgumentNullException.ThrowIfNull(token);

        var propertyName = token.Key;

        foreach (var obj in data)
        {
            if (obj == null)
            {
                continue;
            }

            var value = obj switch
            {
                IDictionary dic => TryGetValueFromDictionary(dic, propertyName),
                IEnumerable<object?> objArray => TryGetValueFromCollection(objArray, propertyName),
                _ => TryGetValueFromObject(obj, propertyName)
            };

            if (value != null)
            {
                return value;
            }
        }

        return null;
    }

    public Task<object?> ResolveAsync(IToken token, params object?[] data)
    {
        return Task.Run(() => this.Resolve(token, data));
    }

    private static object? TryGetValueFromCollection(IEnumerable<object?> collection, string keyName)
    {
        foreach (var item in collection)
        {
            switch (item)
            {
                case null: continue;
                case IEnumerable<object?> objArray:
                {
                    var value = TryGetValueFromCollection(objArray, keyName);
                    if (value is not null)
                    {
                        return value;
                    }

                    break;
                }
                default:
                {
                    var value = TryGetValueFromObject(item, keyName);
                    if (value is not null)
                    {
                        return value;
                    }

                    break;
                }
            }
        }

        return null;
    }

    private static string? TryGetValueFromDictionary(IDictionary dictionary, string keyName)
    {
        if (dictionary is not IDictionary<string, string> objects)
        {
            throw new ArgumentException("Only IDictionary[string, string] is supported", nameof(dictionary));
        }

        var key = objects.Keys.FirstOrDefault(k => k.Equals(keyName, StringComparison.OrdinalIgnoreCase));
        return key is not null ? objects[key] : null;
    }

    private static object? TryGetValueFromObject(object data, string propertyName)
    {
        try
        {
            var prop = data.GetType().GetProperty(
                           propertyName,
                           BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public)
                       ?? data.GetType().GetProperty(
                           propertyName,
                           BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.NonPublic);

            return prop?.GetValue(data);
        }
        catch (ArgumentNullException ex)
        {
            Debug.WriteLine(ex.Message);
            return null;
        }
        catch (AmbiguousMatchException ex)
        {
            Debug.WriteLine(ex.Message);
            return null;
        }
    }

    #endregion
}