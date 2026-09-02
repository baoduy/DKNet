// <copyright file="DiExtensionSurfaceArchitectureTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;
using DKNet.EfCore.Encryption;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace EfCore.Encryption.Tests.Architecture;

/// <summary>
///     Guards the shape of the package's public DI-registration surface. A registration extension must extend
///     the <see cref="IServiceCollection" /> interface, never the concrete <see cref="ServiceCollection" />
///     class: <c>WebApplicationBuilder.Services</c> is typed as the interface, so an extension on the concrete
///     type does not resolve on it and the package's own documented one-liner will not compile in the host it
///     targets. Every other registration extension across the DKNet packages already takes the interface.
/// </summary>
public sealed class DiExtensionSurfaceArchitectureTests
{
    #region Fields

    /// <summary>
    ///     Registration extensions that take the concrete <see cref="ServiceCollection" /> today (DRK-906).
    ///     Now empty — <c>AddEfCoreEncryption</c>, the last entry, was widened to <see cref="IServiceCollection" />,
    ///     so the rule is enforced with no exceptions. Do not add to it.
    /// </summary>
    private static readonly HashSet<string> KnownViolations = [];

    #endregion

    #region Methods

    [Fact]
    public void RegistrationExtensions_ShouldExtendIServiceCollection()
    {
        var offenders = typeof(EfCoreEncryptionSetup).Assembly
            .GetExportedTypes()
            .Where(t => t is { IsAbstract: true, IsSealed: true }) // static classes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.IsDefined(typeof(ExtensionAttribute), false))
            .Where(m => m.GetParameters().FirstOrDefault()?.ParameterType == typeof(ServiceCollection))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var newOffenders = offenders.Where(o => !KnownViolations.Contains(o)).ToArray();

        newOffenders.ShouldBeEmpty(
            "DI registration extensions must take IServiceCollection, not the concrete ServiceCollection — " +
            "WebApplicationBuilder.Services is typed as the interface, so an extension on the concrete class is " +
            "not callable from Program.cs and forces consumers into a cast that throws on any host supplying a " +
            "different IServiceCollection implementation. New offenders: " + string.Join(", ", newOffenders));

        var fixedUp = KnownViolations.Except(offenders, StringComparer.Ordinal).ToArray();

        fixedUp.ShouldBeEmpty(
            "These entries are on the KnownViolations allow-list but no longer violate the rule — that is the fix " +
            "landing. Delete them from KnownViolations so the rule is enforced for them: " +
            string.Join(", ", fixedUp));
    }

    #endregion
}
