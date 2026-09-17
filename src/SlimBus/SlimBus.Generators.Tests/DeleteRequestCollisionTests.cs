using System.Globalization;
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
///     DRK-1448 §3 rows 1-4 proof: the same collision additionally reports an Info diagnostic
///     <c>DKCRUDGEN010</c> naming the entity, the colliding member and the taken request name, so the
///     silent skip above is no longer silent — without ever escalating past Info per-entity.
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

    [Fact]
    public void Run_WithUpdateMemberNamedDelete_ReportsDKCRUDGEN010()
    {
        var (_, diagnostics, result) = GeneratorTestHelper.Run(DomainWithDeleteNamedUpdate, ApiWithAccountDto);

        // Row 1: exactly one DKCRUDGEN010, Info severity, message names the entity, the colliding member
        // and the request name it took — the three substituted values, not the full sentence.
        var diagnostic = diagnostics.Single(d => d.Id == "DKCRUDGEN010");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        message.ShouldContain("Entity 'Account'");
        message.ShouldContain("member 'Delete'");
        message.ShouldContain("request name 'DeleteAccountRequest'");

        // Row 2: the collision's existing behaviour keeps holding alongside the new diagnostic — no
        // Error-severity diagnostics, the 2-arg MapDeleteById fallback still emitted, and the update
        // member's own DeleteAccountRequest record still the only one of that name.
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        var text = GeneratorTestHelper.GeneratedText(result);
        text.Split("record DeleteAccountRequest").Length.ShouldBe(2);
        text.ShouldContain("group.MapDeleteById<global::MyDomain.Account, global::System.Guid>();");
    }

    private const string DomainWithoutDeleteNamedUpdate = """
        using System;
        using DKNet.EfCore.Abstractions.Attributes;
        using DKNet.EfCore.Abstractions.Entities;

        namespace MyDomain
        {
            public class Widget : IEntity<Guid>
            {
                [CrudCreate]
                public Widget(string name) => Name = name;

                [CrudUpdate]
                public void Rename(string name) => Name = name;

                public Guid Id { get; private set; }
                public string Name { get; private set; } = string.Empty;
            }
        }
        """;

    private const string ApiWithWidgetDto = """
        using DKNet.EfCore.DtoGenerator;
        using MyDomain;

        namespace MyApi
        {
            [GenerateDto(typeof(Widget))]
            public partial record WidgetDto;
        }
        """;

    [Fact]
    public void Run_WithoutDeleteRequestNameCollision_ReportsNoDKCRUDGEN010()
    {
        var (_, diagnostics, _) = GeneratorTestHelper.Run(DomainWithoutDeleteNamedUpdate, ApiWithWidgetDto);

        diagnostics.ShouldNotContain(d => d.Id == "DKCRUDGEN010");
    }

    private const string DomainWithOneCollidingEntityAmongTwo = """
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

            public class Widget : IEntity<Guid>
            {
                [CrudCreate]
                public Widget(string name) => Name = name;

                [CrudUpdate]
                public void Rename(string name) => Name = name;

                public Guid Id { get; private set; }
                public string Name { get; private set; } = string.Empty;
            }
        }
        """;

    private const string ApiWithAccountAndWidgetDtos = """
        using DKNet.EfCore.DtoGenerator;
        using MyDomain;

        namespace MyApi
        {
            [GenerateDto(typeof(Account))]
            public partial record AccountDto;

            [GenerateDto(typeof(Widget))]
            public partial record WidgetDto;
        }
        """;

    [Fact]
    public void Run_WithOneCollidingEntityAmongTwo_ReportsDKCRUDGEN010ForThatEntityOnly()
    {
        var (_, diagnostics, _) = GeneratorTestHelper.Run(DomainWithOneCollidingEntityAmongTwo, ApiWithAccountAndWidgetDtos);

        var diagnostic = diagnostics.Single(d => d.Id == "DKCRUDGEN010");
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        message.ShouldContain("Entity 'Account'");
        message.ShouldNotContain("Widget");
    }
}
