namespace EfCore.Extensions.Tests.ArchRules;

/// <summary>
///     TEST-ONLY FIXTURE - not production code, not used by any library type. Exists solely so
///     <see cref="ConsoleUsageArchitectureTests.Rule_CanDetectConsoleUsage_OnADeliberateFixture" /> has a
///     deliberate, harmless dependency on <see cref="System.Console" /> to detect, proving NetArchTest can still
///     see that dependency without relying on a real production offender existing.
/// </summary>
internal static class ConsoleUsageCanaryFixture
{
    /// <summary>
    ///     Deliberately calls <see cref="Console.WriteLine(string)" /> - the sole reason this type exists.
    /// </summary>
    public static void WriteToConsole() => Console.WriteLine("DKNet console-usage architecture canary");
}
