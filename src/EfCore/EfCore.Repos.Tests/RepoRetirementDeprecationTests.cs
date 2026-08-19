using System.Reflection;
using DKNet.EfCore.Repos;
using DKNet.EfCore.Repos.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace EfCore.Repos.Tests;

/// <summary>
///     Proves every publicly reachable type in the retired <c>DKNet.EfCore.Repos</c> /
///     <c>DKNet.EfCore.Repos.Abstractions</c> libraries carries an <see cref="ObsoleteAttribute" /> that names the
///     replacement (<c>DKNet.EfCore.Specifications</c>). <see cref="ObsoleteAttribute" /> is exactly the metadata the
///     compiler reads to emit the CS0618 "deprecated" warning at a consumer's call site — asserting on it here is the
///     runtime-observable proxy for "using this type now produces a deprecation warning".
/// </summary>
public class RepoRetirementDeprecationTests
{
    #region Methods

    public static TheoryData<Type> RetiredTypes => new()
    {
        typeof(IRepository<>),
        typeof(IReadRepository<>),
        typeof(IWriteRepository<>),
        typeof(IRepositoryFactory),
        typeof(ReadRepository<>),
        typeof(WriteRepository<>),
        typeof(Repository<>),
        typeof(RepositoryFactory<>),
        typeof(RepoExtensions),
        typeof(SetupRepository)
    };

    [Theory]
    [MemberData(nameof(RetiredTypes))]
    public void RetiredType_CarriesObsoleteAttribute_PointingAtSpecifications(Type retiredType)
    {
        // Act
        var obsolete = retiredType.GetCustomAttribute<ObsoleteAttribute>();

        // Assert
        obsolete.ShouldNotBeNull($"{retiredType.Name} must carry [Obsolete] so consumers see a build-time warning.");
        obsolete.Message.ShouldNotBeNull();
        obsolete.Message.ShouldContain("DKNet.EfCore.Specifications", Case.Insensitive);
        obsolete.Message.ShouldContain("Migrating-Repos-To-Specifications.md", Case.Insensitive);
    }

    [Fact]
    public void RetiredType_ObsoleteAttribute_IsNotAnError()
    {
        // The libraries stay usable (source-tree only) during migration; [Obsolete(error: true)] would break every
        // existing consumer's build outright instead of giving them a warning to migrate on their own schedule.
        foreach (var type in new[] { typeof(IRepository<>), typeof(IRepositoryFactory), typeof(RepoExtensions) })
            type.GetCustomAttribute<ObsoleteAttribute>()!.IsError.ShouldBeFalse();
    }

    #endregion
}
