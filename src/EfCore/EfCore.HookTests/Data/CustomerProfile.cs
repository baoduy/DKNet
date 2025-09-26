namespace EfCore.HookTests.Data;

public class CustomerProfile : IEntity<Guid>
{
    [Required] public string Name { get; set; }=string.Empty;

    public Guid Id { get; set; } = Guid.Empty;
}