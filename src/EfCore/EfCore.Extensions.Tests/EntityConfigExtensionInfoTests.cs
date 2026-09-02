// <copyright file="EntityConfigExtensionInfoTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.EfCore.Extensions.Internal;

namespace EfCore.Extensions.Tests;

/// <summary>
///     Regression coverage for C14: <see cref="EntityConfigExtensionInfo" /> used to return a constant service
///     provider hash and an always-true <c>ShouldUseSameServiceProvider</c>, so two <c>DbContext</c>s configured
///     with different assembly sets via <c>UseAutoConfigModel(...)</c> could share EF Core's cached model. Both
///     members must now vary with the extension's assembly set, order-independently.
/// </summary>
public class EntityConfigExtensionInfoTests
{
    #region Methods

    [Fact]
    public void GetServiceProviderHashCode_WithDifferentAssemblySets_ReturnsDifferentHashes()
    {
        var register1 = new EntityAutoConfigRegister([typeof(User).Assembly]);
        var register2 = new EntityAutoConfigRegister([typeof(object).Assembly]);

        register1.Info.GetServiceProviderHashCode().ShouldNotBe(register2.Info.GetServiceProviderHashCode());
    }

    [Fact]
    public void GetServiceProviderHashCode_WithSameAssembliesInDifferentOrder_ReturnsSameHash()
    {
        var assemblies = new[] { typeof(User).Assembly, typeof(object).Assembly };
        var register1 = new EntityAutoConfigRegister(assemblies);
        var register2 = new EntityAutoConfigRegister(assemblies.Reverse().ToArray());

        register1.Info.GetServiceProviderHashCode().ShouldBe(register2.Info.GetServiceProviderHashCode());
    }

    [Fact]
    public void ShouldUseSameServiceProvider_WithDifferentAssemblySets_ReturnsFalse()
    {
        var register1 = new EntityAutoConfigRegister([typeof(User).Assembly]);
        var register2 = new EntityAutoConfigRegister([typeof(object).Assembly]);

        register1.Info.ShouldUseSameServiceProvider(register2.Info).ShouldBeFalse();
    }

    [Fact]
    public void ShouldUseSameServiceProvider_WithSameAssemblySetInDifferentOrder_ReturnsTrue()
    {
        var assemblies = new[] { typeof(User).Assembly, typeof(object).Assembly };
        var register1 = new EntityAutoConfigRegister(assemblies);
        var register2 = new EntityAutoConfigRegister(assemblies.Reverse().ToArray());

        register1.Info.ShouldUseSameServiceProvider(register2.Info).ShouldBeTrue();
    }

    #endregion
}
