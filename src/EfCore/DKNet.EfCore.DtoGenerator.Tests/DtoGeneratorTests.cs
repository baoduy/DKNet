using Shouldly;

namespace DKNet.EfCore.DtoGenerator.Tests;

public class DtoGenerationTests
{
    [Fact]
    public void Generated_Source_File_For_PersonDto_Contains_All_Properties()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "GeneratedDtos");
        Directory.Exists(dir).ShouldBeTrue($"GeneratedDtos folder not found at {dir}");
        var file = Path.Combine(dir, "PersonDto.cs");
        File.Exists(file)
            .ShouldBeTrue(
                $"PersonDto.cs not found in {dir}. Files: {string.Join(", ", Directory.GetFiles(dir).Select(Path.GetFileName))}");
        var text = File.ReadAllText(file);

        // Assert presence of each expected property with init-only accessors
        text.ShouldContain("public Guid Id { get; init; }", Case.Sensitive);
        text.ShouldContain("public required string FirstName { get; init; }", Case.Sensitive);
        text.ShouldContain("public string? MiddleName { get; init; }", Case.Sensitive);
        text.ShouldContain("public required string LastName { get; init; }", Case.Sensitive);
        text.ShouldContain("public DateTime CreatedUtc { get; init; }", Case.Sensitive);
        text.ShouldContain("public int Age { get; init; }", Case.Sensitive);

        // Verify it's a partial record
        text.ShouldContain("public partial record PersonDto", Case.Sensitive);

        // Verify NO constructor is generated
        text.ShouldNotContain("public PersonDto(", Case.Sensitive);
    }

    [Fact]
    public void Generated_Source_File_For_CurrencyDataDto_Contains_Properties_From_Unqualified_Typeof()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "GeneratedDtos");
        Directory.Exists(dir).ShouldBeTrue();
        var file = Path.Combine(dir, "CurrencyDataDto.cs");
        File.Exists(file)
            .ShouldBeTrue("CurrencyDataDto.cs should exist when using typeof(CurrencyData) without namespace");
        var text = File.ReadAllText(file);

        text.ShouldContain("public int Id { get; init; }");
        text.ShouldContain("public required string Code { get; init; }");
        text.ShouldContain("public string? Description { get; init; }");

        // Verify NO constructor
        text.ShouldNotContain("public CurrencyDataDto(");
    }

    [Fact]
    public void Generated_Source_File_For_PersonBasicDto_Excludes_Properties()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "GeneratedDtos");
        Directory.Exists(dir).ShouldBeTrue();
        var file = Path.Combine(dir, "PersonBasicDto.cs");
        File.Exists(file).ShouldBeTrue();
        var text = File.ReadAllText(file);

        // Should contain only Id, FirstName, LastName (MiddleName, Age, CreatedUtc are excluded)
        text.ShouldContain("public Guid Id { get; init; }");
        text.ShouldContain("public required string FirstName { get; init; }");
        text.ShouldContain("public required string LastName { get; init; }");

        // Should NOT contain excluded properties
        text.ShouldNotContain("MiddleName");
        text.ShouldNotContain("Age");
        text.ShouldNotContain("CreatedUtc");

        // Verify NO constructor
        text.ShouldNotContain("public PersonBasicDto(");
    }

    [Fact]
    public void Generated_Source_File_For_CustomerDto_Has_Proper_Using_Statements_For_Cross_Namespace_Types()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "GeneratedDtos");
        var file = Path.Combine(dir, "CustomerDto.cs");
        File.Exists(file).ShouldBeTrue();
        var text = File.ReadAllText(file);

        // Should have using statement for entity namespace
        text.ShouldContain("using DKNet.EfCore.DtoEntities;");
        text.ShouldContain("using System.Collections.Generic;");

        // Should have properties with clean type names (no global:: prefix)
        text.ShouldContain("public int CustomerId { get; init; }");
        text.ShouldContain("public required string Name { get; init; }");
        text.ShouldContain("public string? Email { get; init; }");
        text.ShouldContain("public Address? PrimaryAddress { get; init; }");
        text.ShouldContain("public List<Order> Orders { get; init; } = [];");

        // Should have inherited properties from EntityBase
        text.ShouldContain("public DateTime CreatedUtc { get; init; }");
        text.ShouldContain("public DateTime? UpdatedUtc { get; init; }");
        text.ShouldContain("public required string CreatedBy { get; init; }");
        text.ShouldContain("public string? UpdatedBy { get; init; }");

        // Should NOT have global:: prefixes
        text.ShouldNotContain("global::DKNet.EfCore.DtoGenerator.Tests.Order");
        text.ShouldNotContain("global::DKNet.EfCore.DtoGenerator.Tests.Address");
        text.ShouldNotContain("global::System.Collections.Generic.List");

        // Verify NO constructor
        text.ShouldNotContain("public CustomerDto(");
    }

    [Fact]
    public void Generated_Source_File_For_OrderDto_Has_Collection_Property_With_Empty_Initializer()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "GeneratedDtos");
        var file = Path.Combine(dir, "OrderDto.cs");
        File.Exists(file).ShouldBeTrue();
        var text = File.ReadAllText(file);

        // Non-nullable collection should have empty initializer
        text.ShouldContain("public List<OrderItem> Items { get; init; } = [];");

        // Should have using statements for entity namespace
        text.ShouldContain("using DKNet.EfCore.DtoEntities;");
        text.ShouldContain("using System.Collections.Generic;");

        // Should have inherited properties from EntityBase
        text.ShouldContain("public DateTime CreatedUtc { get; init; }");
        text.ShouldContain("public DateTime? UpdatedUtc { get; init; }");
        text.ShouldContain("public required string CreatedBy { get; init; }");
        text.ShouldContain("public string? UpdatedBy { get; init; }");

        // Verify NO constructor
        text.ShouldNotContain("public OrderDto(");
    }

    [Fact]
    public void Generated_Source_File_For_PersonCustomDto_Does_Not_Duplicate_Existing_Properties()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "GeneratedDtos");
        var file = Path.Combine(dir, "PersonCustomDto.cs");
        File.Exists(file).ShouldBeTrue();
        var text = File.ReadAllText(file);

        // Should NOT duplicate FirstName (it's already defined in Dtos.cs)
        // Count occurrences of "FirstName" - should only be in existing property definition
        var firstNameCount = System.Text.RegularExpressions.Regex.Matches(text, "FirstName").Count;
        firstNameCount.ShouldBeLessThan(2, "FirstName should not be duplicated in generated code");

        // Should contain other properties
        text.ShouldContain("public Guid Id { get; init; }");
        text.ShouldContain("public string? MiddleName { get; init; }");
        text.ShouldContain("public required string LastName { get; init; }");
    }

    [Fact]
    public void Generated_DTOs_Use_Required_Keyword_For_NonNullable_Strings()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "GeneratedDtos");
        var personFile = Path.Combine(dir, "PersonDto.cs");
        File.Exists(personFile).ShouldBeTrue();
        var text = File.ReadAllText(personFile);

        // Non-nullable strings should have 'required' keyword
        text.ShouldContain("public required string FirstName");
        text.ShouldContain("public required string LastName");

        // Nullable strings should NOT have 'required' keyword
        text.ShouldContain("public string? MiddleName");
        text.ShouldNotContain("public required string? MiddleName");
    }

    [Fact]
    public void Generated_DTOs_Are_AutoGenerated_And_Have_Nullable_Enable()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "GeneratedDtos");
        var file = Path.Combine(dir, "PersonDto.cs");
        File.Exists(file).ShouldBeTrue();
        var text = File.ReadAllText(file);

        // Should have auto-generated comment
        text.ShouldContain("// <auto-generated/> Generated by DKNet.EfCore.DtoGenerator");

        // Should have nullable enable
        text.ShouldContain("#nullable enable");
    }
}