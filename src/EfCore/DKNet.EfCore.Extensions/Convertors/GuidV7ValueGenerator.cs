using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace DKNet.EfCore.Extensions.Convertors;

public sealed class GuidV7ValueGenerator : ValueGenerator<Guid>
{
    #region Properties

    public override bool GeneratesTemporaryValues => false;

    #endregion

    #region Methods

    public override Guid Next(EntityEntry entry) => Guid.NewGuid(); // Replace with compatible method

    #endregion
}