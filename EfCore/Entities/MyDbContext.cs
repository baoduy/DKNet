using Microsoft.EntityFrameworkCore;

namespace EfCore.TestDataLayer;

public class MyDbContext(DbContextOptions options) : DbContext(options);