using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

/// <summary>
///     DRK-1326 §5 scenario 8 (@unit): every record type in the generated set gets its own delete request.
///     Frozen at `at_sha` once approved.
/// </summary>
public class DeleteRequestEmissionTests
{
    private const string DomainWithTwoEntities = """
        using System;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class AccountGroup : IEntity<Guid>
            {
                [CrudCreate]
                public AccountGroup(string name) => Name = name;

                public Guid Id { get; private set; }
                public string Name { get; private set; } = string.Empty;
            }

            public class Account : IEntity<Guid>
            {
                [CrudCreate]
                public Account(string name) => Name = name;

                public Guid Id { get; private set; }
                public string Name { get; private set; } = string.Empty;
            }
        }
        """;

    private const string ApiWithBothDtos = """
        using DKNet.EfCore.DtoGenerator;
        using MyDomain;

        namespace MyApi
        {
            [GenerateDto(typeof(AccountGroup))]
            public partial record AccountGroupDto;

            [GenerateDto(typeof(Account))]
            public partial record AccountDto;
        }
        """;

    [Fact]
    public void Run_WithTwoEntitiesInGeneratedSet_EachGetsItsOwnDeleteRequestCarryingItsKey()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithTwoEntities, ApiWithBothDtos);

        var text = GeneratorTestHelper.GeneratedText(result);
        text.ShouldContain("sealed partial record DeleteAccountGroupRequest");
        text.ShouldContain("sealed partial record DeleteAccountRequest");
        text.ShouldContain("IWithKey<global::System.Guid>");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }
}
