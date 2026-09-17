using System.Reflection;
using DKNet.SlimBus.Extensions.Handlers;

namespace SlimBus.Extensions.Tests;

/// <summary>
///     DRK-1477: guards that the <c>DKNet.SlimBus.Extensions</c> public surface stops carrying the
///     retired acting-user record and every other <see cref="ObsoleteAttribute" /> member once stage 2
///     deletes them. Matches by <see cref="MemberInfo.Name" />/<see cref="Type.Name" /> rather than a
///     compile-time reference so this file keeps compiling after that deletion.
/// </summary>
public class PublicSurfaceGuardTests
{
    #region Methods

    private static readonly Assembly SlimBusExtensionsAssembly = typeof(SlimBusEventPublisher).Assembly;

    // DeclaredOnly: a type inheriting an Obsolete member from elsewhere is not this assembly's surface
    // to fix. Property/event accessor methods are skipped (IsSpecialName) so a [Obsolete] property is
    // reported once, on the PropertyInfo, not duplicated on its get_/set_ methods.
    private static IEnumerable<MemberInfo> PublicOrProtectedMembers(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                    BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var member in type.GetMembers(flags))
        {
            if (member is MethodInfo { IsSpecialName: true }) continue;

            var isVisible = member switch
            {
                MethodBase method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly,
                FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
                PropertyInfo property => property.GetAccessors(true)
                    .Any(a => a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly),
                EventInfo @event => @event.GetAddMethod(true) is { } adder &&
                                     (adder.IsPublic || adder.IsFamily || adder.IsFamilyOrAssembly),
                _ => false,
            };

            if (isVisible) yield return member;
        }
    }

    [Fact]
    public void RetiredActingUserRecord_IsNotExported()
    {
        // retired_actingUserRecord_isNotExported: the assembly must export no type named RequestBase —
        // matched on Type.Name against the full exported-type list, so a rename within the same
        // namespace still fails this guard.
        var exportedTypeNames = SlimBusExtensionsAssembly.GetExportedTypes().Select(t => t.Name).ToArray();

        exportedTypeNames.ShouldNotContain(
            "RequestBase",
            $"{SlimBusExtensionsAssembly.GetName().Name} must no longer export a type named RequestBase; " +
            $"exported types: {string.Join(", ", exportedTypeNames)}");
    }

    [Fact]
    public void PublicSurface_CarriesNoObsoleteMember()
    {
        // publicSurface_carriesNoObsoleteMember: no exported type, and no public/protected member it
        // declares, may carry [Obsolete] once the retired record and its guidance are gone.
        var violations = new List<string>();

        foreach (var type in SlimBusExtensionsAssembly.GetExportedTypes())
        {
            if (type.GetCustomAttribute<ObsoleteAttribute>() is not null) violations.Add(type.FullName ?? type.Name);

            foreach (var member in PublicOrProtectedMembers(type))
                if (member.GetCustomAttribute<ObsoleteAttribute>() is not null)
                    violations.Add($"{type.FullName ?? type.Name}.{member.Name}");
        }

        violations.ShouldBeEmpty(
            $"No public or protected member of {SlimBusExtensionsAssembly.GetName().Name} may carry " +
            $"[Obsolete]; offenders: {string.Join(", ", violations)}");
    }

    #endregion
}
