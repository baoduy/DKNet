using Microsoft.EntityFrameworkCore;

namespace EfCore.TestDataLayer;

[ExcludeFromCodeCoverage]
public class MyDbContext(DbContextOptions options) : DbContext(options);