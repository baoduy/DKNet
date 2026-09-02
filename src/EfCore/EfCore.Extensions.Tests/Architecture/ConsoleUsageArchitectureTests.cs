using DKNet.EfCore.Extensions.Extensions;
using NetArchTest.Rules;

namespace EfCore.Extensions.Tests.ArchRules;

/// <summary>
///     Enforces that library code in <c>DKNet.EfCore.Extensions</c> never writes diagnostics through
///     <see cref="System.Console" />. A framework primitive that writes to <c>Console</c> bypasses the host's
///     logging configuration entirely — it cannot be filtered, redirected, redacted, or suppressed — and pollutes
///     stdout in production. Diagnostics must go through an injected <c>ILogger</c> instead.
///     <para>
///         This is a Tier-2 baseline rule (architecture-review DRK-73). The former known offender,
///         <see cref="EfCoreExceptionHandler" />, was migrated to <c>ILogger&lt;EfCoreExceptionHandler&gt;</c>, so
///         the allow-list is now empty and the rule covers the whole assembly. Do not add names to it.
///     </para>
/// </summary>
public sealed class ConsoleUsageArchitectureTests
{
    #region Fields

    /// <summary>
    ///     Known offenders. Must stay empty — never add a name here to silence a new violation.
    /// </summary>
    private static readonly string[] KnownViolations = [];

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

    #endregion
}
