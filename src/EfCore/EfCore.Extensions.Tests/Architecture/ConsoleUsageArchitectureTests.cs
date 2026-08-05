using DKNet.EfCore.Extensions.Extensions;
using NetArchTest.Rules;

namespace EfCore.Extensions.Tests.ArchRules;

/// <summary>
///     Enforces that library code in <c>DKNet.EfCore.Extensions</c> never writes diagnostics through
///     <see cref="System.Console" />. A framework primitive that writes to <c>Console</c> bypasses the host's
///     logging configuration entirely — it cannot be filtered, redirected, redacted, or suppressed — and pollutes
///     stdout in production. Diagnostics must go through an injected <c>ILogger</c> instead.
///     <para>
///         This is a Tier-2 baseline rule (architecture-review DRK-73): there is exactly one known offender today,
///         <see cref="EfCoreExceptionHandler" /> (<c>EfCoreExceptionHandler.cs:61</c>), which is on the allow-list
///         below. The allow-list must only ever SHRINK — when the offender is migrated to <c>ILogger</c>, delete its
///         entry so the rule covers the whole assembly. Do not add new names to it.
///     </para>
/// </summary>
public sealed class ConsoleUsageArchitectureTests
{
    #region Fields

    /// <summary>
    ///     Today's known offenders. Must only shrink — never add a name here to silence a new violation.
    /// </summary>
    private static readonly string[] KnownViolations = [nameof(EfCoreExceptionHandler)];

    #endregion

    #region Methods

    [Fact]
    public void ProductionTypes_ExceptKnownOffenders_MustNotDependOnSystemConsole()
    {
        var result = Types.InAssembly(typeof(EfCoreExceptionHandler).Assembly)
            .That()
            .DoNotHaveName(KnownViolations)
            .Should()
            .NotHaveDependencyOn("System.Console")
            .GetResult();

        var offenders = result.FailingTypeNames ?? [];
        result.IsSuccessful.ShouldBeTrue(
            "Library code must emit diagnostics through an injected ILogger, never System.Console, so hosts can " +
            "filter/redirect/redact/suppress them. New offenders: " + string.Join(", ", offenders) +
            ". Fix by injecting ILogger<T> instead of calling Console.*; do not add the type to the KnownViolations allow-list.");
    }

    [Fact]
    public void Rule_CanDetectConsoleUsage_OnTheKnownOffender()
    {
        // Self-check: the allow-listed offender genuinely uses System.Console. If this ever passes, the rule above
        // has gone blind (NetArchTest can no longer see the dependency) and would silently stop enforcing anything.
        var result = Types.InAssembly(typeof(EfCoreExceptionHandler).Assembly)
            .That()
            .HaveName(nameof(EfCoreExceptionHandler))
            .Should()
            .NotHaveDependencyOn("System.Console")
            .GetResult();

        result.IsSuccessful.ShouldBeFalse(
            "The known offender EfCoreExceptionHandler must still be detected as depending on System.Console; " +
            "if this assertion fails the enforcement rule can no longer see Console usage and is worthless.");
    }

    #endregion
}
