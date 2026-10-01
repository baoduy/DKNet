namespace DKNet.EfCore.Extensions.Internal;

/// <summary>
///     Adds the version of the distinct set of builders registered through
///     <see cref="EfCoreSetup.AddGlobalModelBuilder{TImplementation}" /> to the inner model cache key, so a builder
///     registered after a model was cached makes the next context build a new model.
/// </summary>
internal sealed class GlobalModelBuilderCacheKeyFactory(IModelCacheKeyFactory inner) : IModelCacheKeyFactory
{
    #region Methods

    public object Create(DbContext context, bool designTime) =>
        (inner.Create(context, designTime), EfCoreSetup.GlobalModelBuildersVersion);

    #endregion
}
