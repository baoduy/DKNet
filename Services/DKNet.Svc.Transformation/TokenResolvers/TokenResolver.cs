using System.Collections;
using System.Diagnostics;
using System.Reflection;
using DKNet.Svc.Transformation.TokenExtractors;

namespace DKNet.Svc.Transformation.TokenResolvers;

public class TokenResolver : ITokenResolver
{
    /// <summary>
    /// Get the first not null value of the public property of data.
    /// </summary>
    /// <param name="token"></param>
    /// <param name="data"></param>
    /// <exception cref="ArgumentException"></exception>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">if data or token is null</exception>
    public virtual object? Resolve(IToken token, params object?[] data)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (data is not { Length: > 0 })
            return null;

        var propertyName = token.Key;

        foreach (var obj in data)
        {
            if (obj == null) continue;

            object? value = null;

            switch (obj)
            {
                case IDictionary:
                    {
                        if (obj is not IDictionary<string, object> objects)
                            throw new ArgumentException("Only IDictionary[string, object] is supported",
                                paramName: nameof(data));

                        var key = objects.Keys.FirstOrDefault(k => k.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
                        value = key is not null ? objects[key] : null;
                        break;
                    }
                case IEnumerable<object?> objArray:
                    {
                        foreach (var item in objArray)
                        {
                            if (item is null) continue;
                            value = GetProperty(item, propertyName)?.GetValue(item);
                            if (value is not null) break;
                        }

                        break;
                    }
                default:
                    value = GetProperty(obj, propertyName)?.GetValue(obj);
                    break;
            }

            if (value != null) return value;
        }

        return null;
    }

    public Task<object?> ResolveAsync(IToken token, params object?[] data) => Task.Run(() => Resolve(token, data));

    protected virtual PropertyInfo? GetProperty(object data, string propertyName)
    {
        try
        {
            return data.GetType().GetProperty(propertyName,
                       BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public)

                   //BindingFlags.NonPublic and BindingFlags.Public are not work together
                   ?? data.GetType().GetProperty(propertyName,
                       BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.NonPublic);
        }
        catch (ArgumentNullException ex)
        {
            Debug.WriteLine(ex.Message);
            return null;
        }catch(AmbiguousMatchException ex){
            Debug.WriteLine(ex.Message);
             return null;
        }
    }
}