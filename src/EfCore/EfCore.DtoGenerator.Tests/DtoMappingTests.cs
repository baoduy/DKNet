using System.ComponentModel.DataAnnotations;
using DKNet.EfCore.DtoEntities;
using DKNet.EfCore.DtoEntities.Features.Merchants;
using DKNet.EfCore.DtoEntities.Features.StaticData;
using DKNet.EfCore.DtoEntities.Share;
using EfCore.DtoGenerator.Tests.Features;
using EfCore.DtoGenerator.Tests.Features.Merchants;
using EfCore.DtoGenerator.Tests.Features.StaticData.ChannelDatas;
using EfCore.DtoGenerator.Tests.Features.StaticData.Currencies;
using Mapster;
using Shouldly;

namespace EfCore.DtoGenerator.Tests;

public class DtoMappingTests
{
    #region Methods

    [Fact]
    public void AllDtos_ShouldHaveConsistentPropertyMapping()
    {
        // This test verifies that Mapster can map all DTOs without configuration
        // If properties don't match, Mapster will throw or return null values

        // Arrange
        var currency = new CurrencyData("EUR", "Euro", false, "European Currency");
        var channel = ChannelData.Create(
            ChannelCodes.CreditCard,
            "Test",
            "T+1",
            "US",
            "USD",
            1,
            1000,
            "sys"
        );
        var merchantChannel = new MerchantChannel(Guid.NewGuid(), ChannelCodes.Wallet, "T+1", 10, 1000, "admin");

        // Act & Assert - Should not throw
        var currencyDto = currency.Adapt<CurrencyDto>();
        var channelDto = channel.Adapt<ChannelDto>();
        var merchantChannelDto = merchantChannel.Adapt<MerchantChannelDto>();

        currencyDto.ShouldNotBeNull();
        channelDto.ShouldNotBeNull();
        merchantChannelDto.ShouldNotBeNull();
    }

    [Fact]
    public void ChannelDto_ShouldHaveMaxLengthAttributes_OnStringProperties()
    {
        // Arrange & Act
        var countryProperty = typeof(ChannelDto).GetProperty("Country");
        var currencyProperty = typeof(ChannelDto).GetProperty("Currency");
        var nameProperty = typeof(ChannelDto).GetProperty("Name");
        var codeProperty = typeof(ChannelDto).GetProperty("Code");

        // Assert
        countryProperty.ShouldNotBeNull();
        var countryMaxLength = countryProperty!.GetCustomAttributes(typeof(MaxLengthAttribute), false)
            .Cast<MaxLengthAttribute>()
            .FirstOrDefault();
        countryMaxLength.ShouldNotBeNull();
        countryMaxLength!.Length.ShouldBe(3);

        currencyProperty.ShouldNotBeNull();
        var currencyMaxLength = currencyProperty!.GetCustomAttributes(typeof(MaxLengthAttribute), false)
            .Cast<MaxLengthAttribute>()
            .FirstOrDefault();
        currencyMaxLength.ShouldNotBeNull();
        currencyMaxLength!.Length.ShouldBe(4);

        nameProperty.ShouldNotBeNull();
        var nameMaxLength = nameProperty!.GetCustomAttributes(typeof(MaxLengthAttribute), false)
            .Cast<MaxLengthAttribute>()
            .FirstOrDefault();
        nameMaxLength.ShouldNotBeNull();
        nameMaxLength!.Length.ShouldBe(50);

        codeProperty.ShouldNotBeNull();
        var codeMaxLength = codeProperty!.GetCustomAttributes(typeof(MaxLengthAttribute), false)
            .Cast<MaxLengthAttribute>()
            .FirstOrDefault();
        codeMaxLength.ShouldNotBeNull();
        codeMaxLength!.Length.ShouldBe(10);
    }

    [Fact]
    public void ChannelDto_ShouldMapAllProperties_FromChannelData()
    {
        // Arrange
        var entity = ChannelData.Create(
            ChannelCodes.QrQris,
            "Credit Card Channel",
            "T+1",
            "US",
            "USD",
            10.00m,
            10000.00m,
            "system"
        );

        // Act
        var dto = entity.Adapt<ChannelDto>();

        // Assert
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(entity.Id);
        dto.Code.ShouldBe(entity.Code);
        dto.Country.ShouldBe(entity.Country);
        dto.Currency.ShouldBe(entity.Currency);
        dto.Name.ShouldBe(entity.Name);
        dto.Settlement.ShouldBe(entity.Settlement);
        dto.MinAmount.ShouldBe(entity.MinAmount);
        dto.MaxAmount.ShouldBe(entity.MaxAmount);
        dto.CreatedBy.ShouldBe(entity.CreatedBy);
        dto.CreatedOn.ShouldBe(entity.CreatedOn);
        dto.LastModifiedBy.ShouldBe(entity.LastModifiedBy);
        dto.LastModifiedOn.ShouldBe(entity.LastModifiedOn);
    }

    [Fact]
    public void ChannelDto_ShouldMapAllProperties_WithNullMaxAmount()
    {
        // Arrange
        var entity = ChannelData.Create(
            ChannelCodes.BankTransfer,
            "Bank Transfer",
            "T+2",
            "UK",
            "GBP",
            50.00m,
            null,
            "admin"
        );

        // Act
        var dto = entity.Adapt<ChannelDto>();

        // Assert
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(entity.Id);
        dto.MaxAmount.ShouldBeNull();
        dto.MinAmount.ShouldBe(entity.MinAmount);
    }

    [Fact]
    public void CurrencyDto_ShouldHaveMaxLengthAttributes_OnStringProperties()
    {
        // Arrange & Act
        var codeProperty = typeof(CurrencyDto).GetProperty("Code");
        var nameProperty = typeof(CurrencyDto).GetProperty("Name");
        var descriptionProperty = typeof(CurrencyDto).GetProperty("Description");

        // Assert
        codeProperty.ShouldNotBeNull();
        var codeMaxLength = codeProperty!.GetCustomAttributes(typeof(MaxLengthAttribute), false)
            .Cast<MaxLengthAttribute>()
            .FirstOrDefault();
        codeMaxLength.ShouldNotBeNull();
        codeMaxLength!.Length.ShouldBe(10);

        nameProperty.ShouldNotBeNull();
        var nameMaxLength = nameProperty!.GetCustomAttributes(typeof(MaxLengthAttribute), false)
            .Cast<MaxLengthAttribute>()
            .FirstOrDefault();
        nameMaxLength.ShouldNotBeNull();
        nameMaxLength!.Length.ShouldBe(50);

        descriptionProperty.ShouldNotBeNull();
        var descriptionMaxLength = descriptionProperty!.GetCustomAttributes(typeof(MaxLengthAttribute), false)
            .Cast<MaxLengthAttribute>()
            .FirstOrDefault();
        descriptionMaxLength.ShouldNotBeNull();
        descriptionMaxLength!.Length.ShouldBe(200);
    }

    [Fact]
    public void CurrencyDto_ShouldMapAllProperties_FromCurrencyData()
    {
        // Arrange
        var entity = new CurrencyData(
            "USD",
            "US Dollar",
            false,
            "United States Dollar"
        );

        // Act
        var dto = entity.Adapt<CurrencyDto>();

        // Assert
        dto.ShouldNotBeNull();
        dto.Code.ShouldBe(entity.Code);
        dto.Name.ShouldBe(entity.Name);
        dto.IsCrypto.ShouldBe(entity.IsCrypto);
        dto.Description.ShouldBe(entity.Description);
        dto.Id.ShouldBe(entity.Id);
    }

    [Fact]
    public void CurrencyDto_ShouldMapAllProperties_WithNullDescription()
    {
        // Arrange
        var entity = new CurrencyData(
            "BTC",
            "Bitcoin",
            true
        );

        // Act
        var dto = entity.Adapt<CurrencyDto>();

        // Assert
        dto.ShouldNotBeNull();
        dto.Code.ShouldBe(entity.Code);
        dto.Name.ShouldBe(entity.Name);
        dto.IsCrypto.ShouldBe(entity.IsCrypto);
        dto.Description.ShouldBeNull();
        dto.Id.ShouldBe(entity.Id);
    }

    [Theory]
    [InlineData("USD", "US Dollar", true)]
    [InlineData("EUR", "Euro", false)]
    [InlineData("BTC", "Bitcoin", true)]
    public void CurrencyDto_ShouldMapCorrectly_ForMultipleCurrencies(string code, string name, bool isCrypto)
    {
        // Arrange
        var entity = new CurrencyData(code, name, isCrypto, $"Description for {name}");

        // Act
        var dto = entity.Adapt<CurrencyDto>();

        // Assert
        dto.Code.ShouldBe(code);
        dto.Name.ShouldBe(name);
        dto.IsCrypto.ShouldBe(isCrypto);
    }

    [Fact]
    public void MerchantBalanceDto_ShouldHaveComplexReferenceType_WithNullInitializer()
    {
        // This test verifies that the generated DTO has the complex reference type property
        // with the = null!; initializer to avoid CS8618 compiler errors

        // Act - Verify the property exists with proper type
        var merchantProperty = typeof(MerchantBalanceDto).GetProperty("Merchant");

        // Assert
        merchantProperty.ShouldNotBeNull("Merchant property should be generated in the DTO");
        merchantProperty.PropertyType.ShouldBe(typeof(Merchant));
        merchantProperty.CanRead.ShouldBeTrue();
        merchantProperty.SetMethod.ShouldNotBeNull("Property should have init accessor");
    }

    [Fact]
    public void MerchantChannelDto_ShouldMapAllProperties_FromMerchantChannel()
    {
        // Arrange
        var entity = new MerchantChannel(
            Guid.NewGuid(),
            ChannelCodes.QrQris,
            "T+1",
            10.00m,
            5000.00m,
            "admin"
        );

        // Act
        var dto = entity.Adapt<MerchantChannelDto>();

        // Assert
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(entity.Id);
        dto.MerchantId.ShouldBe(entity.MerchantId);
        dto.Code.ShouldBe(entity.Code);
        dto.Settlement.ShouldBe(entity.Settlement);
        dto.MinAmount.ShouldBe(entity.MinAmount);
        dto.MaxAmount.ShouldBe(entity.MaxAmount);
        dto.CreatedBy.ShouldBe(entity.CreatedBy);
        dto.CreatedOn.ShouldBe(entity.CreatedOn);
        dto.LastModifiedBy.ShouldBe(entity.LastModifiedBy);
        dto.LastModifiedOn.ShouldBe(entity.LastModifiedOn);
    }

    [Fact]
    public void MerchantChannelDto_ShouldMapAllProperties_WithNullMaxAmount()
    {
        // Arrange
        var entity = new MerchantChannel(
            Guid.NewGuid(),
            ChannelCodes.BankTransfer,
            "T+3",
            100.00m,
            null,
            "admin"
        );

        // Act
        var dto = entity.Adapt<MerchantChannelDto>();

        // Assert
        dto.ShouldNotBeNull();
        dto.MaxAmount.ShouldBeNull();
        dto.MinAmount.ShouldBe(entity.MinAmount);
    }

    [Theory]
    [InlineData(ChannelCodes.CreditCard, "T+1", 10.00, 5000.00)]
    [InlineData(ChannelCodes.BankTransfer, "T+2", 50.00, null)]
    [InlineData(ChannelCodes.Wallet, "T+0", 1.00, 10000.00)]
    public void MerchantChannelDto_ShouldMapCorrectly_ForDifferentChannelTypes(
        ChannelCodes code, string settlement, decimal minAmount, object? maxAmount)
    {
        // Arrange
        var entity = new MerchantChannel(Guid.NewGuid(), code, settlement, minAmount,
            maxAmount is not null ? decimal.Parse(maxAmount.ToString()!) : null, "admin");

        // Act
        var dto = entity.Adapt<MerchantChannelDto>();

        // Assert
        dto.Code.ShouldBe(code);
        dto.Settlement.ShouldBe(settlement);
        dto.MinAmount.ShouldBe(minAmount);
        dto.MaxAmount.ShouldBe(maxAmount);
    }

    [Fact]
    public void MerchantChannelDto_ShouldNotIncludeExcludedProperties()
    {
        // Arrange
        var entity = new MerchantChannel(
            Guid.NewGuid(),
            ChannelCodes.Wallet,
            "T+0",
            1.00m,
            null,
            "system"
        );

        // Act
        var dto = entity.Adapt<MerchantChannelDto>();

        // Assert - Verify Merchant property is excluded
        dto.ShouldNotBeNull();
        typeof(MerchantChannelDto).GetProperty("Merchant").ShouldBeNull();
    }

    [Fact]
    public void PersonNameDto_ShouldMapOnlyIncludedProperties_FromPerson()
    {
        // Arrange
        var entity = new Person("John", "Middle", "Doe", 30);

        // Act
        var dto = entity.Adapt<PersonNameDto>();

        // Assert
        dto.ShouldNotBeNull();
        dto.FirstName.ShouldBe(entity.FirstName);
        dto.LastName.ShouldBe(entity.LastName);

        // Verify only included properties exist
        typeof(PersonNameDto).GetProperty("FirstName").ShouldNotBeNull();
        typeof(PersonNameDto).GetProperty("LastName").ShouldNotBeNull();
        typeof(PersonNameDto).GetProperty("Id").ShouldBeNull();
        typeof(PersonNameDto).GetProperty("MiddleName").ShouldBeNull();
        typeof(PersonNameDto).GetProperty("Age").ShouldBeNull();
        typeof(PersonNameDto).GetProperty("CreatedUtc").ShouldBeNull();
    }

    [Fact]
    public void PersonNameDto_ShouldNotIncludeExcludedProperties()
    {
        // Arrange & Act - Verify excluded properties don't exist at compile time
        var hasId = typeof(PersonNameDto).GetProperty("Id") != null;
        var hasMiddleName = typeof(PersonNameDto).GetProperty("MiddleName") != null;
        var hasAge = typeof(PersonNameDto).GetProperty("Age") != null;
        var hasCreatedUtc = typeof(PersonNameDto).GetProperty("CreatedUtc") != null;

        // Assert - None of these properties should exist
        hasId.ShouldBeFalse();
        hasMiddleName.ShouldBeFalse();
        hasAge.ShouldBeFalse();
        hasCreatedUtc.ShouldBeFalse();
    }

    [Fact]
    public void PersonSummaryDto_ShouldMapOnlyIncludedProperties_FromPerson()
    {
        // Arrange
        var entity = new Person("Jane", "M", "Smith", 25);

        // Act
        var dto = entity.Adapt<PersonSummaryDto>();

        // Assert
        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(entity.Id);
        dto.FirstName.ShouldBe(entity.FirstName);
        dto.LastName.ShouldBe(entity.LastName);
        dto.Age.ShouldBe(entity.Age);

        // Verify only included properties exist
        typeof(PersonSummaryDto).GetProperty("Id").ShouldNotBeNull();
        typeof(PersonSummaryDto).GetProperty("FirstName").ShouldNotBeNull();
        typeof(PersonSummaryDto).GetProperty("LastName").ShouldNotBeNull();
        typeof(PersonSummaryDto).GetProperty("Age").ShouldNotBeNull();
        typeof(PersonSummaryDto).GetProperty("MiddleName").ShouldBeNull();
        typeof(PersonSummaryDto).GetProperty("CreatedUtc").ShouldBeNull();
    }

    [Fact]
    public void TestProductDto_ShouldHaveAllValidationAttributes()
    {
        // Test Required attributes
        var nameProperty = typeof(TestProductDto).GetProperty("Name");
        nameProperty.ShouldNotBeNull();
        nameProperty!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();

        var skuProperty = typeof(TestProductDto).GetProperty("Sku");
        skuProperty.ShouldNotBeNull();
        skuProperty!.GetCustomAttributes(typeof(RequiredAttribute), false).ShouldNotBeEmpty();

        // Test StringLength attribute with MinimumLength
        var stringLengthAttr = nameProperty.GetCustomAttributes(typeof(StringLengthAttribute), false)
            .Cast<StringLengthAttribute>()
            .FirstOrDefault();
        stringLengthAttr.ShouldNotBeNull();
        stringLengthAttr!.MaximumLength.ShouldBe(100);
        stringLengthAttr.MinimumLength.ShouldBe(3);

        // Test MaxLength attribute
        var maxLengthAttr = skuProperty.GetCustomAttributes(typeof(MaxLengthAttribute), false)
            .Cast<MaxLengthAttribute>()
            .FirstOrDefault();
        maxLengthAttr.ShouldNotBeNull();
        maxLengthAttr!.Length.ShouldBe(50);

        // Test Range attributes
        var priceProperty = typeof(TestProductDto).GetProperty("Price");
        priceProperty.ShouldNotBeNull();
        var priceRangeAttr = priceProperty!.GetCustomAttributes(typeof(RangeAttribute), false)
            .Cast<RangeAttribute>()
            .FirstOrDefault();
        priceRangeAttr.ShouldNotBeNull();
        priceRangeAttr!.Minimum.ShouldBe(0.01);
        priceRangeAttr.Maximum.ShouldBe(999999.99);

        var stockProperty = typeof(TestProductDto).GetProperty("StockQuantity");
        stockProperty.ShouldNotBeNull();
        var stockRangeAttr = stockProperty!.GetCustomAttributes(typeof(RangeAttribute), false)
            .Cast<RangeAttribute>()
            .FirstOrDefault();
        stockRangeAttr.ShouldNotBeNull();
        stockRangeAttr!.Minimum.ShouldBe(0);
        stockRangeAttr.Maximum.ShouldBe(10000);

        // Test EmailAddress attribute
        var emailProperty = typeof(TestProductDto).GetProperty("Email");
        emailProperty.ShouldNotBeNull();
        emailProperty!.GetCustomAttributes(typeof(EmailAddressAttribute), false).ShouldNotBeEmpty();

        // Test Url attribute
        var urlProperty = typeof(TestProductDto).GetProperty("WebsiteUrl");
        urlProperty.ShouldNotBeNull();
        urlProperty!.GetCustomAttributes(typeof(UrlAttribute), false).ShouldNotBeEmpty();

        // Test Phone attribute
        var phoneProperty = typeof(TestProductDto).GetProperty("PhoneNumber");
        phoneProperty.ShouldNotBeNull();
        phoneProperty!.GetCustomAttributes(typeof(PhoneAttribute), false).ShouldNotBeEmpty();
    }

    [Fact]
    public void TestProductDto_ValidationAttributes_ShouldFailForInvalidData()
    {
        // Arrange - Create DTO with invalid data
        var invalidProduct = new TestProductDto
        {
            Id = Guid.NewGuid(),
            Name = "AB", // Too short (MinimumLength = 3)
            Sku = "", // Required but empty
            Price = -10.00m, // Out of range (min 0.01)
            StockQuantity = 20000, // Out of range (max 10000)
            Email = "invalid-email", // Invalid email format
            CreatedDate = DateTime.UtcNow,
            IsActive = true
        };

        var validationContext = new ValidationContext(invalidProduct);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(invalidProduct, validationContext, validationResults, true);

        // Assert
        isValid.ShouldBeFalse();
        validationResults.ShouldNotBeEmpty();

        // Should have validation errors for Name, Sku, Price, StockQuantity, and Email
        validationResults.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void TestProductDto_ValidationAttributes_ShouldValidateCorrectly()
    {
        // Arrange
        var validProduct = new TestProductDto
        {
            Id = Guid.NewGuid(),
            Name = "Valid Product",
            Sku = "SKU123",
            Price = 99.99m,
            StockQuantity = 100,
            Email = "test@example.com",
            Description = "A valid product description",
            WebsiteUrl = new Uri("https://example.com"),
            PhoneNumber = "123-456-7890",
            CreatedDate = DateTime.UtcNow,
            IsActive = true
        };

        var validationContext = new ValidationContext(validProduct);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(validProduct, validationContext, validationResults, true);

        // Assert
        isValid.ShouldBeTrue();
        validationResults.Count.ShouldBe(0);
    }

    #endregion
}