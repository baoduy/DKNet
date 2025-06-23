namespace EfCore.HookTests.Data;

public class CustomerProfile : IEntity<Guid>
{
    [Required]
    public string Name { get; set; }

    public Guid Id { get; set; } = Guid.Empty;
}