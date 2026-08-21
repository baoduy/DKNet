// <copyright file="ServiceBusExtensionsTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.ServiceBus;
using Shouldly;

namespace Aspire.Hosting.ServiceBus.Tests;

/// <summary>
///     Guards the invariant that the Azure Service Bus emulator hosting extension keeps depending on its
///     SQL Server backing store (DRK-642 §3): the vendor emulator image requires SQL Server as its own
///     backing store, so <see cref="ServiceBusExtensions.AddServiceBus" /> must keep wiring a wait dependency
///     on the SQL Server resource it is given, regardless of how the extension's implementation evolves.
/// </summary>
public class ServiceBusExtensionsTests
{
    #region Methods

    /// <summary>
    ///     Registering the Service Bus emulator against a SQL Server resource must make the emulator wait
    ///     for that exact SQL Server resource to be ready before it starts - the emulator has no functioning
    ///     backing store otherwise.
    /// </summary>
    [Fact]
    public void AddServiceBus_GivenSqlServerResource_WaitsForThatSqlServerBeforeStarting()
    {
        // Arrange
        var builder = DistributedApplication.CreateBuilder();
        var sqlServer = builder.AddSqlServer("sql");
        var configFilePath = Path.GetTempFileName();

        try
        {
            // Act
            var serviceBus = builder.AddServiceBus(sqlServer, configFilePath);

            // Assert
            serviceBus.Resource.Annotations.OfType<WaitAnnotation>()
                .ShouldContain(
                    wait => ReferenceEquals(wait.Resource, sqlServer.Resource),
                    "the emulator must wait on the exact SQL Server resource it was given before starting");
        }
        finally
        {
            File.Delete(configFilePath);
        }
    }

    #endregion
}
