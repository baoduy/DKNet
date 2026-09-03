// <copyright file="EfCoreExtensionsShadowKeyTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace EfCore.Extensions.Tests;

/// <summary>
///     Regression coverage for C10: <c>GetEntityKeyValues</c> / <c>GetPrimaryKeyValues</c> previously
///     dereferenced <c>IProperty.PropertyInfo!</c>, which is <c>null</c> for shadow properties. An entity whose
///     primary key is configured purely as a shadow property (no matching CLR property) used to throw a
///     <see cref="NullReferenceException" />; both methods now read via <c>CurrentValues</c> instead.
/// </summary>
public class EfCoreExtensionsShadowKeyTests
{
    #region Nested types

    private sealed class ShadowKeyEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ShadowKeyDbContext(DbContextOptions<ShadowKeyDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ShadowKeyEntity>(b =>
            {
                b.Property<int>("Id");
                b.HasKey("Id");
            });
        }
    }

    #endregion

    #region Methods

    [Fact]
    public async Task GetEntityKeyValues_WithShadowPrimaryKey_ReturnsValueWithoutThrowing()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ShadowKeyDbContext>()
            .UseInMemoryDatabase("ShadowKey_EntityEntry_" + Guid.NewGuid())
            .Options;
        await using var context = new ShadowKeyDbContext(options);

        var entity = new ShadowKeyEntity { Name = "Test" };
        context.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var keyValues = context.Entry(entity).GetEntityKeyValues();

        // Assert
        keyValues.ShouldHaveSingleItem();
        keyValues["Id"].ShouldNotBeNull();
    }

    [Fact]
    public async Task GetPrimaryKeyValues_WithShadowPrimaryKey_ReturnsValueWithoutThrowing()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ShadowKeyDbContext>()
            .UseInMemoryDatabase("ShadowKey_PrimaryKeyValues_" + Guid.NewGuid())
            .Options;
        await using var context = new ShadowKeyDbContext(options);

        var entity = new ShadowKeyEntity { Name = "Test" };
        context.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var keyValues = context.GetPrimaryKeyValues(entity);

        // Assert
        keyValues.ShouldHaveSingleItem();
        keyValues["Id"].ShouldNotBeNull();
    }

    #endregion
}
