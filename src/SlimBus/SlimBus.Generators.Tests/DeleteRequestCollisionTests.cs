using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace SlimBus.Generators.Tests;

/// <summary>
///     DRK-1326 §3 row 6 proof: a create/update/action member that already resolves to the name
///     <c>Delete{Entity}Request</c> suppresses the generated delete request and the 3-arg map call falls
///     back to the plain 2-arg <c>MapDeleteById&lt;TEntity, TKey&gt;</c> — the compilation stays clean rather
///     than emitting a duplicate <c>DeleteAccountRequest</c> type.
/// </summary>
public class DeleteRequestCollisionTests
{
    private const string DomainWithDeleteNamedUpdate = """
        using System;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class Account : IEntity<Guid>
            {
                [CrudCreate]
                public Account(string name) => Name = name;

                [CrudUpdate]
                public void Delete() => Name = string.Empty;

                public Guid Id { get; private set; }
                public string Name { get; private set; } = string.Empty;
            }
        }
        """;

    private const string ApiWithAccountDto = """
        using DKNet.EfCore.DtoGenerator;
        using MyDomain;

        namespace MyApi
        {
            [GenerateDto(typeof(Account))]
            public partial record AccountDto;
        }
        """;

    [Fact]
    public void Run_WithUpdateMemberNamedDelete_SkipsGeneratedDeleteRequestAndFallsBackToTwoArgMapCall()
    {
        var (output, _, result) = GeneratorTestHelper.Run(DomainWithDeleteNamedUpdate, ApiWithAccountDto);

        var text = GeneratorTestHelper.GeneratedText(result);
        // The update member named "Delete" still emits its own DeleteAccountRequest (an update request, with
        // an Id property AND the update's own params) — collision guard means that's the ONLY
        // DeleteAccountRequest emitted, so it appears exactly once.
        text.Split("record DeleteAccountRequest").Length.ShouldBe(2);
        text.ShouldContain("group.MapDeleteById<global::MyDomain.Account, global::System.Guid>();");
        text.ShouldNotContain("group.MapDeleteById<global::MyDomain.Account, global::System.Guid, DeleteAccountRequest>();");
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
    }
}
