using DKNet.Svc.Transformation.Convertors;
using DKNet.Svc.Transformation.TokenExtractors;
using DKNet.Svc.Transformation.TokenResolvers;

namespace DKNet.Svc.Transformation;

/// <summary>
///     Configuration options for the template transformation service.
/// </summary>
/// <remarks>
///     Purpose: Provides centralized configuration for all aspects of template transformation.
///     Rationale: Enables customization of token extraction, value formatting, and caching behavior.
///     Functionality:
///     - Controls local caching behavior for transformed tokens
///     - Configures value formatting for different token types
///     - Manages token extractors for different bracket styles
///     - Handles token resolution from various data sources
///     - Provides global parameters shared across transformations
///     Integration:
///     - Used by TransformerService as the primary configuration source
///     - Supports dependency injection for customization
///     Best Practices:
///     - Configure extractors based on your template format needs
///     - Use global parameters for application-wide data
///     - Enable caching for performance in high-throughput scenarios
///     - Customize formatters for specific value presentation requirements
/// </remarks>
public class TransformOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether local caching is disabled.
    /// </summary>
    /// <value>
    ///     <c>true</c> if local caching is disabled; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    ///     When enabled (false), token values are cached internally for subsequent use, improving performance
    ///     for repeated transformations with the same tokens. Disable caching when token values change
    ///     frequently or when memory usage is a concern.
    /// </remarks>
    public bool DisabledLocalCache { get; set; } = true;

    /// <summary>
    ///     Gets or sets the value formatter used to format token values before applying them to templates.
    /// </summary>
    /// <value>
    ///     An <see cref="IValueFormatter" /> instance. Default is <see cref="ValueFormatter" />.
    /// </value>
    /// <remarks>
    ///     The formatter controls how token values are converted to strings in the final template output.
    ///     Custom formatters can be provided to handle specific formatting requirements such as
    ///     date formats, number formatting, or custom object serialization.
    /// </remarks>
    public IValueFormatter Formatter { get; set; } = new ValueFormatter();

    /// <summary>
    ///     Gets the collection of token extractors used to identify and extract tokens from templates.
    /// </summary>
    /// <value>
    ///     A collection of <see cref="ITokenExtractor" /> instances. Default includes extractors for
    ///     angled brackets (&lt;token&gt;), square brackets ([token]), and curly brackets ({token}).
    /// </value>
    /// <remarks>
    ///     Token extractors define the syntax patterns that the transformation service recognizes
    ///     as replaceable tokens in templates. You can add custom extractors to support additional
    ///     token formats or remove default extractors if not needed.
    /// </remarks>
    public ICollection<ITokenExtractor> TokenExtractors { get; } =
        [new AngledBracketTokenExtractor(), new SquareBracketExtractor(), new CurlyBracketExtractor()];

    /// <summary>
    ///     Gets or sets the token resolver used to find values for tokens from provided data sources.
    /// </summary>
    /// <value>
    ///     An <see cref="ITokenResolver" /> instance. Default is <see cref="TokenResolver" />.
    /// </value>
    /// <remarks>
    ///     The token resolver determines how token names are matched against properties or keys
    ///     in the provided data objects. Custom resolvers can implement different matching
    ///     strategies such as case-insensitive matching, path-based resolution, or custom
    ///     property mapping logic.
    /// </remarks>
    public ITokenResolver TokenResolver { get; set; } = new TokenResolver();

    /// <summary>
    ///     Gets or sets the global parameters that are shared across all transformation operations.
    /// </summary>
    /// <value>
    ///     An enumerable collection of objects containing global data. Default is an empty collection.
    /// </value>
    /// <remarks>
    ///     Global parameters provide a way to share common data across all transformation operations
    ///     without having to pass the same data repeatedly. These are typically configured at
    ///     application startup and contain application-wide settings, constants, or frequently
    ///     accessed data objects. Global parameters are checked after instance-specific parameters
    ///     during token resolution.
    /// </remarks>
    public IEnumerable<object> GlobalParameters { get; set; } = [];
}