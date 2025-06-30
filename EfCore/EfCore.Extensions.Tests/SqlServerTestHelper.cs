namespace EfCore.Extensions.Tests;

public static class SqlServerTestHelper
{
    public static DbContextOptionsBuilder UseSqlServerTestContainer(this DbContextOptionsBuilder @this,
        string connectionString)
    {
        if (@this is null)
            throw new ArgumentNullException(nameof(@this));

        @this.UseSqlServer(connectionString);
        return @this;
    }
}