using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.TestDataLayer.Mappers;

internal enum PersionTypes
{
    Director = 1,
    Employee = 2,
}

internal class PersonMapper : AuditEntityMapper<Person>
{
    #region Methods

    public override void Configure(EntityTypeBuilder<Person> builder)
    {
        base.Configure(builder);

        builder.Property<int>("PersionType").IsRequired();

        builder.HasDiscriminator<int>("PersionType")
            .HasValue<Director>((int) PersionTypes.Director)
            .HasValue<Employee>((int) PersionTypes.Employee);
    }

    #endregion Methods
}