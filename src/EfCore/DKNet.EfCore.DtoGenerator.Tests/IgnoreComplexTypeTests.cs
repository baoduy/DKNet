using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoGenerator.Tests.Features;
using Shouldly;

namespace DKNet.EfCore.DtoGenerator.Tests;

/// <summary>
///     Tests for IgnoreComplexType functionality in the DtoGenerator.
/// </summary>
public class IgnoreComplexTypeTests
{
    #region Methods

    [Fact]
    public void CustomerDto_PropertyTypes_ShouldBeCorrect()
    {
        // Verify the property types are as expected
        var customerIdProperty = typeof(CustomerDto).GetProperty("CustomerId");
        var nameProperty = typeof(CustomerDto).GetProperty("Name");
        var emailProperty = typeof(CustomerDto).GetProperty("Email");
        var primaryAddressProperty = typeof(CustomerDto).GetProperty("PrimaryAddress");
        var ordersProperty = typeof(CustomerDto).GetProperty("Orders");

        customerIdProperty.ShouldNotBeNull();
        customerIdProperty!.PropertyType.ShouldBe(typeof(int));

        nameProperty.ShouldNotBeNull();
        nameProperty!.PropertyType.ShouldBe(typeof(string));

        emailProperty.ShouldNotBeNull();
        emailProperty!.PropertyType.ShouldBe(typeof(string));

        primaryAddressProperty.ShouldNotBeNull();
        primaryAddressProperty!.PropertyType.ShouldBe(typeof(Address));

        ordersProperty.ShouldNotBeNull();
        ordersProperty!.PropertyType.ShouldBe(typeof(ICollection<Order>));
    }

    [Fact]
    public void CustomerDto_WithoutIgnoreComplexType_ShouldIncludeAllProperties()
    {
        // Assert - Verify all properties exist including complex types
        var hasCustomerId = typeof(CustomerDto).GetProperty("CustomerId") != null;
        var hasName = typeof(CustomerDto).GetProperty("Name") != null;
        var hasEmail = typeof(CustomerDto).GetProperty("Email") != null;
        var hasPrimaryAddress = typeof(CustomerDto).GetProperty("PrimaryAddress") != null;
        var hasOrders = typeof(CustomerDto).GetProperty("Orders") != null;
        var hasCreatedUtc = typeof(CustomerDto).GetProperty("CreatedUtc") != null;
        var hasUpdatedUtc = typeof(CustomerDto).GetProperty("UpdatedUtc") != null;

        // Assert - All properties should exist
        hasCustomerId.ShouldBeTrue("CustomerId should be included");
        hasName.ShouldBeTrue("Name should be included");
        hasEmail.ShouldBeTrue("Email should be included");
        hasPrimaryAddress.ShouldBeTrue("PrimaryAddress should be included (complex type)");
        hasOrders.ShouldBeTrue("Orders should be included (complex collection type)");
        hasCreatedUtc.ShouldBeTrue("CreatedUtc should be included");
        hasUpdatedUtc.ShouldBeTrue("UpdatedUtc should be included");
    }

    [Fact]
    public void CustomerSimpleDto_ShouldExcludeBothSingleAndCollectionComplexTypes()
    {
        // This test specifically verifies that both single entity properties (PrimaryAddress)
        // and collection properties (Orders) are excluded when IgnoreComplexType is true

        // Assert - Verify single entity complex type is excluded
        var hasPrimaryAddress = typeof(CustomerSimpleDto).GetProperty("PrimaryAddress") != null;
        hasPrimaryAddress.ShouldBeFalse("PrimaryAddress (single entity) should be excluded");

        // Assert - Verify collection complex type is excluded
        var hasOrders = typeof(CustomerSimpleDto).GetProperty("Orders") != null;
        hasOrders.ShouldBeFalse("Orders (collection) should be excluded");
    }

    [Fact]
    public void CustomerSimpleDto_WithIgnoreComplexType_ShouldExcludeComplexTypes()
    {
        // Assert - Verify simple properties exist
        var hasCustomerId = typeof(CustomerSimpleDto).GetProperty("CustomerId") != null;
        var hasName = typeof(CustomerSimpleDto).GetProperty("Name") != null;
        var hasEmail = typeof(CustomerSimpleDto).GetProperty("Email") != null;
        var hasCreatedUtc = typeof(CustomerSimpleDto).GetProperty("CreatedUtc") != null;
        var hasUpdatedUtc = typeof(CustomerSimpleDto).GetProperty("UpdatedUtc") != null;

        // Assert - Verify complex types are excluded
        var hasPrimaryAddress = typeof(CustomerSimpleDto).GetProperty("PrimaryAddress") != null;
        var hasOrders = typeof(CustomerSimpleDto).GetProperty("Orders") != null;

        // Assert - Simple properties should exist
        hasCustomerId.ShouldBeTrue("CustomerId should be included");
        hasName.ShouldBeTrue("Name should be included");
        hasEmail.ShouldBeTrue("Email should be included");
        hasCreatedUtc.ShouldBeTrue("CreatedUtc should be included");
        hasUpdatedUtc.ShouldBeTrue("UpdatedUtc should be included");

        // Assert - Complex types should be excluded
        hasPrimaryAddress.ShouldBeFalse("PrimaryAddress should be excluded (complex type)");
        hasOrders.ShouldBeFalse("Orders should be excluded (complex collection type)");
    }

    [Fact]
    public void CustomerWithExcludeDto_ShouldCombineIgnoreComplexTypeAndExclude()
    {
        // Assert - Verify properties exist or don't exist
        var hasCustomerId = typeof(CustomerWithExcludeDto).GetProperty("CustomerId") != null;
        var hasName = typeof(CustomerWithExcludeDto).GetProperty("Name") != null;
        var hasEmail = typeof(CustomerWithExcludeDto).GetProperty("Email") != null;
        var hasCreatedUtc = typeof(CustomerWithExcludeDto).GetProperty("CreatedUtc") != null;
        var hasUpdatedUtc = typeof(CustomerWithExcludeDto).GetProperty("UpdatedUtc") != null;
        var hasPrimaryAddress = typeof(CustomerWithExcludeDto).GetProperty("PrimaryAddress") != null;
        var hasOrders = typeof(CustomerWithExcludeDto).GetProperty("Orders") != null;

        // Assert - Properties that should exist
        hasCustomerId.ShouldBeTrue("CustomerId should be included");
        hasName.ShouldBeTrue("Name should be included");
        hasCreatedUtc.ShouldBeTrue("CreatedUtc should be included");
        hasUpdatedUtc.ShouldBeTrue("UpdatedUtc should be included");

        // Assert - Properties that should be excluded (Email by Exclude, complex types by IgnoreComplexType)
        hasEmail.ShouldBeFalse("Email should be excluded by Exclude parameter");
        hasPrimaryAddress.ShouldBeFalse("PrimaryAddress should be excluded by IgnoreComplexType");
        hasOrders.ShouldBeFalse("Orders should be excluded by IgnoreComplexType");
    }

    [Fact]
    public void CustomerWithIncludeDto_ShouldIgnoreIgnoreComplexTypeFlag()
    {
        // Assert - Verify only included properties exist
        var hasCustomerId = typeof(CustomerWithIncludeDto).GetProperty("CustomerId") != null;
        var hasName = typeof(CustomerWithIncludeDto).GetProperty("Name") != null;
        var hasOrders = typeof(CustomerWithIncludeDto).GetProperty("Orders") != null;

        // Assert - Verify other properties are excluded
        var hasEmail = typeof(CustomerWithIncludeDto).GetProperty("Email") != null;
        var hasPrimaryAddress = typeof(CustomerWithIncludeDto).GetProperty("PrimaryAddress") != null;
        var hasCreatedUtc = typeof(CustomerWithIncludeDto).GetProperty("CreatedUtc") != null;
        var hasUpdatedUtc = typeof(CustomerWithIncludeDto).GetProperty("UpdatedUtc") != null;

        // Assert - Only included properties should exist
        hasCustomerId.ShouldBeTrue("CustomerId should be included");
        hasName.ShouldBeTrue("Name should be included");
        hasOrders.ShouldBeTrue("Orders should be included (Include overrides IgnoreComplexType)");

        // Assert - All other properties should not exist
        hasEmail.ShouldBeFalse("Email should not be included");
        hasPrimaryAddress.ShouldBeFalse("PrimaryAddress should not be included");
        hasCreatedUtc.ShouldBeFalse("CreatedUtc should not be included");
        hasUpdatedUtc.ShouldBeFalse("UpdatedUtc should not be included");
    }

    #endregion
}