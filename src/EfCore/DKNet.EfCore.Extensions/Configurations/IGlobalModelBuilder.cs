namespace DKNet.EfCore.Extensions.Configurations;

/// <summary>
///     This will be scans from the Assemblies when registering the services.
///     Use this to apply the global filter for entity or add custom entities into DbContext.
/// </summary>
public interface IGlobalModelBuilder
{
    #region Methods

    void Apply(ModelBuilder modelBuilder, DbContext context);

    #endregion
}