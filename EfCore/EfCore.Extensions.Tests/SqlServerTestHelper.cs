namespace EfCore.Extensions.Tests;

public static class SqlServerTestHelper
{
    public static DbContextOptionsBuilder UseSqlServerTestContainer(this DbContextOptionsBuilder @this,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(@this);

        @this.UseSqlServer(connectionString);
        return @this;
    }
}