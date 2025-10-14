using DKNet.EfCore.DtoEntities.Features.Merchants;
using DKNet.EfCore.DtoEntities.Features.StaticData;
using DKNet.EfCore.DtoEntities.Share;
using DKNet.EfCore.DtoGenerator.Tests.Features.Merchants;
using DKNet.EfCore.DtoGenerator.Tests.Features.StaticData.ChannelDatas;
using DKNet.EfCore.DtoGenerator.Tests.Features.StaticData.Currencies;
using Mapster;
using Shouldly;

namespace DKNet.EfCore.DtoGenerator.Tests;

public class DtoMappingTests
{
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
            true,
            null
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

    [Theory]
    [InlineData(ChannelCodes.CreditCard, "T+1", 10.00, 5000.00)]
    [InlineData(ChannelCodes.BankTransfer, "T+2", 50.00, null)]
    [InlineData(ChannelCodes.Wallet, "T+0", 1.00, 10000.00)]
    public void MerchantChannelDto_ShouldMapCorrectly_ForDifferentChannelTypes(
        ChannelCodes code, string settlement, decimal minAmount, decimal? maxAmount)
    {
        // Arrange
        var entity = new MerchantChannel(Guid.NewGuid(), code, settlement, minAmount, maxAmount, "admin");

        // Act
        var dto = entity.Adapt<MerchantChannelDto>();

        // Assert
        dto.Code.ShouldBe(code);
        dto.Settlement.ShouldBe(settlement);
        dto.MinAmount.ShouldBe(minAmount);
        dto.MaxAmount.ShouldBe(maxAmount);
    }
}