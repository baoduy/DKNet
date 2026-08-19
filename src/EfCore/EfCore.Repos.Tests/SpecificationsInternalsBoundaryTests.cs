using System.Reflection;
using System.Runtime.CompilerServices;
using DKNet.EfCore.Specifications;

namespace EfCore.Repos.Tests;

/// <summary>
///     <c>DKNet.EfCore.Specifications</c> used to grant <c>DKNet.EfCore.Repos</c> an
///     <see cref="InternalsVisibleToAttribute" /> so the retired library could reuse internal query-building helpers.
///     Retirement dropped that grant — <c>DKNet.EfCore.Repos</c> now carries local copies of those helpers instead.
///     The pre-existing test-only grants (<c>EfCore.Repos.Tests</c>, <c>EfCore.Specifications.Tests</c>) are
///     unrelated to the retirement and stay.
/// </summary>
public class SpecificationsInternalsBoundaryTests
{
    #region Methods

    [Fact]
    public void SpecificationsAssembly_NoLongerGrantsInternalsToTheRetiredRepoLibrary()
    {
        var grants = typeof(IRepositorySpec).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .ToList();

        grants.ShouldNotContain(
            "DKNet.EfCore.Repos",
            $"DKNet.EfCore.Specifications must not grant InternalsVisibleTo the retired DKNet.EfCore.Repos post-retirement. Found grants: {string.Join(", ", grants)}");
    }

    #endregion
}
