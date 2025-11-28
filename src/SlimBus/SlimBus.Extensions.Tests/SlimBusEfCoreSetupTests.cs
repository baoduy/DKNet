// <copyright file="SlimBusEfCoreSetupTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.SlimBus.Extensions.Interceptors;

namespace SlimBus.Extensions.Tests
{
    /// <summary>
    ///     Unit tests for SlimBusEfCoreSetup extension methods.
    /// </summary>
    public class SlimBusEfCoreSetupTests
    {
        /// <summary>
        ///     Verifies AddSlimBusEfCoreExceptionHandler registers the correct handler keyed by DbContext type.
        /// </summary>
        [Fact]
        public void AddSlimBusEfCoreExceptionHandler_RegistersKeyedHandler_CanResolveByKey()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSlimBusEfCoreExceptionHandler<TestDbContext, TestExceptionHandler>();
            var provider = services.BuildServiceProvider();
            var key = typeof(TestDbContext).FullName;

            // Act
            var handler = provider.GetKeyedService<IEfCoreExceptionHandler>(key!);

            // Assert
            handler.ShouldNotBeNull();
            handler.ShouldBeOfType<TestExceptionHandler>();
        }

        private class TestExceptionHandler : IEfCoreExceptionHandler
        {
            public Task<EfConcurrencyResolution> HandlingAsync(DbContext context, DbUpdateConcurrencyException exception, CancellationToken cancellationToken = default)
                => Task.FromResult(EfConcurrencyResolution.IgnoreChanges);
        }
    }
}
