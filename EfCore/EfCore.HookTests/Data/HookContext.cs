namespace EfCore.HookTests.Data;

public class HookContext(DbContextOptions<HookContext> options) : DbContext(options);