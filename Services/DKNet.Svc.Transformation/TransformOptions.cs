using DKNet.Svc.Transformation.Convertors;
using DKNet.Svc.Transformation.TokenExtractors;
using DKNet.Svc.Transformation.TokenResolvers;

namespace DKNet.Svc.Transformation;

public class TransformOptions
{
    /// <summary>
    /// The token value will be cached internally for subsequence use. If don't want the value to be cached. You can disable the cache here.
    /// </summary>
    public bool DisabledLocalCache { get; set; } = true;

    /// <summary>
    /// The <see cref="IValueFormatter"/> to format the value of Token before apply to the template.
    /// </summary>
    public IValueFormatter Formatter { get; set; } = new ValueFormatter();

    /// <summary>
    /// The <see cref="ITokenExtractor"/> for templates.
    /// </summary>
    public ICollection<ITokenExtractor> TokenExtractors { get; } = [new AngledBracketTokenExtractor(), new SquareBracketExtractor(), new CurlyBracketExtractor()];

    /// <summary>
    /// The <see cref="ITokenResolver"/> for all <see cref="IToken"/>
    /// </summary>
    public ITokenResolver TokenResolver { get; set; } = new TokenResolver();

    /// <summary>
    /// Global Data object that share to all transforming.
    /// There are some global data which sharing across application shall be configured here when app start
    /// </summary>
    public IEnumerable<object> GlobalParameters { get; set; } = [];
}