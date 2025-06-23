namespace SlimBus.Extensions.Tests.Data;

public class TestEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }= Guid.Empty;

    [MaxLength(500)] public string Name { get; set; } = null!;
}