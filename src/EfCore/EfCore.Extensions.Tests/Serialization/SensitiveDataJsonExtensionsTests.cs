// New tests to cover role-gated sensitive-property JSON filtering (DRK-1183).

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DKNet.EfCore.Extensions.Serialization;

namespace EfCore.Extensions.Tests.Serialization;

public sealed class Product
{
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }

    [SensitiveData("pricing", "audit")]
    public decimal SupplierCostPrice { get; init; }

    [SensitiveData]
    public string SupplierReferenceCode { get; init; } = string.Empty;

    // No [SensitiveData] — even though the name resembles a sensitive one, only the explicit
    // attribute gates role-aware response filtering (DRK-1183 §4 resolved decision).
    public string SupplierApiKey { get; init; } = string.Empty;
}

public sealed class PlainModel
{
    public string Value { get; init; } = string.Empty;
}

public class SensitiveDataJsonExtensionsTests
{
    #region Methods

    [Fact]
    public void Serialize_CallerHoldingDeclaredRole_ReceivesSensitiveProperty()
    {
        // Arrange
        var options = new JsonSerializerOptions().UseRoleAwareSensitiveData(
            new FakeSensitiveDataPrincipalAccessor(AuthenticatedAs("pricing")));

        // Act
        var json = JsonSerializer.Serialize(BuildProduct(), options);

        // Assert
        json.ShouldContain("\"SupplierCostPrice\":412.50");
    }

    [Fact]
    public void Serialize_CallerWithoutDeclaredRole_OmitsSensitiveProperty_ButKeepsRest()
    {
        // Arrange
        var options = new JsonSerializerOptions().UseRoleAwareSensitiveData(
            new FakeSensitiveDataPrincipalAccessor(AuthenticatedAs("support")));

        // Act
        var json = JsonSerializer.Serialize(BuildProduct(), options);

        // Assert
        json.ShouldNotContain("SupplierCostPrice");
        json.ShouldContain("\"Name\":\"Espresso Machine\"");
        json.ShouldContain("\"Price\":899");
    }

    [Fact]
    public void Serialize_CallerHoldingOneOfSeveralPermittedRoles_ReceivesSensitiveProperty()
    {
        // Arrange
        var options = new JsonSerializerOptions().UseRoleAwareSensitiveData(
            new FakeSensitiveDataPrincipalAccessor(AuthenticatedAs("audit")));

        // Act
        var json = JsonSerializer.Serialize(BuildProduct(), options);

        // Assert
        json.ShouldContain("\"SupplierCostPrice\":412.50");
    }

    [Fact]
    public void Serialize_RoleLessDeclaration_AdmitsAnyAuthenticatedCaller()
    {
        // Arrange
        var options = new JsonSerializerOptions().UseRoleAwareSensitiveData(
            new FakeSensitiveDataPrincipalAccessor(AuthenticatedAs("support")));

        // Act
        var json = JsonSerializer.Serialize(BuildProduct(), options);

        // Assert
        json.ShouldContain("\"SupplierReferenceCode\":\"SUP-4417\"");
    }

    [Fact]
    public void Serialize_RoleLessDeclaration_StillWithholdsFromUnauthenticatedCaller()
    {
        // Arrange
        var options = new JsonSerializerOptions().UseRoleAwareSensitiveData(
            new FakeSensitiveDataPrincipalAccessor(Unauthenticated()));

        // Act
        var json = JsonSerializer.Serialize(BuildProduct(), options);

        // Assert
        json.ShouldNotContain("SupplierReferenceCode");
    }

    [Fact]
    public void Serialize_NoCallerIdentityAvailable_WithholdsSensitiveProperty()
    {
        // Arrange
        var options = new JsonSerializerOptions().UseRoleAwareSensitiveData(
            new FakeSensitiveDataPrincipalAccessor(null));

        // Act
        var json = JsonSerializer.Serialize(BuildProduct(), options);

        // Assert
        json.ShouldNotContain("SupplierCostPrice");
    }

    [Fact]
    public void Serialize_TwoCallersOfOneOptionsInstance_AreJudgedIndependently()
    {
        // Arrange
        var accessor = new FakeSensitiveDataPrincipalAccessor(AuthenticatedAs("pricing"));
        var options = new JsonSerializerOptions().UseRoleAwareSensitiveData(accessor);

        // Act
        var miaJson = JsonSerializer.Serialize(BuildProduct(), options);
        accessor.Current = AuthenticatedAs("support");
        var rajJson = JsonSerializer.Serialize(BuildProduct(), options);

        // Assert
        miaJson.ShouldContain("\"SupplierCostPrice\":412.50");
        rajJson.ShouldNotContain("SupplierCostPrice");
    }

    [Fact]
    public void Serialize_HostNotOptedIn_SensitivePropertyUnaffected()
    {
        // Arrange
        var options = new JsonSerializerOptions();

        // Act
        var json = JsonSerializer.Serialize(BuildProduct(), options);

        // Assert
        json.ShouldContain("\"SupplierCostPrice\":412.50");
    }

    [Fact]
    public void Serialize_PropertySensitiveOnlyByName_StillReturned()
    {
        // Arrange
        var options = new JsonSerializerOptions().UseRoleAwareSensitiveData(
            new FakeSensitiveDataPrincipalAccessor(null));

        // Act
        var json = JsonSerializer.Serialize(BuildProduct(), options);

        // Assert
        json.ShouldContain("\"SupplierApiKey\":\"sup-key-8842\"");
    }

    [Fact]
    public void Serialize_ModelWithNoSensitiveProperty_ByteForByteUnchangedByOptIn()
    {
        // Arrange
        var plain = new PlainModel { Value = "hello" };

        // Act
        var withFilter = JsonSerializer.Serialize(
            plain, new JsonSerializerOptions().UseRoleAwareSensitiveData(new FakeSensitiveDataPrincipalAccessor(null)));
        var baseline = JsonSerializer.Serialize(plain, new JsonSerializerOptions());

        // Assert
        withFilter.ShouldBe(baseline);
    }

    [Fact]
    public void UseRoleAwareSensitiveData_NullOptions_Throws()
    {
        // Arrange
        JsonSerializerOptions? options = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(
            () => options!.UseRoleAwareSensitiveData(new FakeSensitiveDataPrincipalAccessor(null)));
    }

    [Fact]
    public void UseRoleAwareSensitiveData_NullAccessor_Throws()
    {
        // Arrange
        var options = new JsonSerializerOptions();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => options.UseRoleAwareSensitiveData(null!));
    }

    [Fact]
    public void UseRoleAwareSensitiveData_ComposesWithExistingResolver_RatherThanReplacingIt()
    {
        // Arrange
        var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        // Act
        options.UseRoleAwareSensitiveData(new FakeSensitiveDataPrincipalAccessor(AuthenticatedAs("pricing")));
        var json = JsonSerializer.Serialize(BuildProduct(), options);

        // Assert
        json.ShouldContain("\"SupplierCostPrice\":412.50");
    }

    [Fact]
    public void UseRoleAwareSensitiveData_OnAlreadyFrozenOptions_ThrowsInvalidOperationException()
    {
        // Arrange — a first serialize call freezes the options instance
        var options = new JsonSerializerOptions();
        JsonSerializer.Serialize(42, options);

        // Act & Assert
        Should.Throw<InvalidOperationException>(
            () => options.UseRoleAwareSensitiveData(new FakeSensitiveDataPrincipalAccessor(null)));
    }

    [Fact]
    public void IsPermitted_UnauthenticatedPrincipal_ReturnsFalse()
    {
        // Act & Assert
        SensitiveDataJsonExtensions.IsPermitted(Unauthenticated(), ["pricing"]).ShouldBeFalse();
    }

    [Fact]
    public void IsPermitted_NullPrincipal_ReturnsFalse()
    {
        // Act & Assert
        SensitiveDataJsonExtensions.IsPermitted(null, ["pricing"]).ShouldBeFalse();
    }

    [Fact]
    public void IsPermitted_AuthenticatedWithZeroRoleClaims_AndNoRoleNamed_ReturnsTrue()
    {
        // Act & Assert
        SensitiveDataJsonExtensions.IsPermitted(AuthenticatedAs(), []).ShouldBeTrue();
    }

    [Fact]
    public void IsPermitted_AuthenticatedWithZeroRoleClaims_AndRoleNamed_ReturnsFalse()
    {
        // Act & Assert
        SensitiveDataJsonExtensions.IsPermitted(AuthenticatedAs(), ["pricing"]).ShouldBeFalse();
    }

    #endregion

    #region Internals

    private static ClaimsPrincipal AuthenticatedAs(params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static ClaimsPrincipal Unauthenticated() => new(new ClaimsIdentity());

    private static Product BuildProduct() => new()
    {
        Name = "Espresso Machine",
        Price = 899.00m,
        SupplierCostPrice = 412.50m,
        SupplierReferenceCode = "SUP-4417",
        SupplierApiKey = "sup-key-8842"
    };

    private sealed class FakeSensitiveDataPrincipalAccessor(ClaimsPrincipal? current) : ISensitiveDataPrincipalAccessor
    {
        public ClaimsPrincipal? Current { get; set; } = current;
    }

    #endregion
}
