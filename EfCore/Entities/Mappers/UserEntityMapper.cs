namespace EfCore.TestDataLayer.Mappers;

internal class UserEntityMapper : AuditEntityMapper<User>
{
    #region Public Methods

    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);
    }

    #endregion Public Methods
}