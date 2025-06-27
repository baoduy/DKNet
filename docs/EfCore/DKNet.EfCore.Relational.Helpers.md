# DKNet.EfCore.Relational.Helpers

**Relational database utilities and helper methods for Entity Framework Core that provide streamlined database operations, connection management, and database-specific optimizations while supporting Domain-Driven Design (DDD) and Onion Architecture principles.**

## What is this project?

DKNet.EfCore.Relational.Helpers provides a collection of utility methods and helper functions specifically designed for relational database operations with Entity Framework Core. It simplifies common database tasks, provides connection management utilities, and offers database-specific optimizations that help developers work more efficiently with relational databases while maintaining clean architecture patterns.

### Key Features

- **DbContextHelpers**: Comprehensive utilities for DbContext management and operations
- **Connection Management**: Robust database connection handling and pooling utilities
- **Database Operations**: Streamlined methods for common database tasks
- **Performance Optimizations**: Database-specific performance enhancements
- **Bulk Operations**: Efficient bulk insert, update, and delete operations
- **Schema Management**: Utilities for database schema operations and migrations
- **Query Optimization**: Helper methods for optimizing complex queries
- **Transaction Management**: Enhanced transaction handling capabilities
- **Database Diagnostics**: Tools for monitoring and debugging database operations

## How it contributes to DDD and Onion Architecture

### Infrastructure Layer Utilities

DKNet.EfCore.Relational.Helpers provides **Infrastructure Layer utilities** that enhance database operations without affecting domain logic or creating dependencies in higher layers:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Benefits from: Improved performance, reliable operations      │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  Benefits from: Efficient bulk operations, transaction helpers │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  🏷️ Completely unaware of helper utilities                     │
│  📋 Pure business logic without database concerns              │
│  🎭 Domain operations remain technology-agnostic               │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Database Utilities, Optimizations)           │
│                                                                 │
│  🔧 DbContextHelpers - Context management utilities            │
│  ⚡ Performance helpers - Query and operation optimization     │
│  🗃️ Bulk operation helpers - Efficient data processing         │
│  📊 Connection management - Robust connection handling         │
│  🔍 Schema utilities - Database structure management           │
│  📈 Diagnostic tools - Monitoring and debugging helpers        │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Clean Separation**: Database utilities isolated from domain logic
2. **Performance Consistency**: Consistent database operation patterns across aggregates
3. **Transaction Support**: Proper transaction boundaries for aggregate operations
4. **Bulk Processing**: Efficient handling of large domain data sets
5. **Monitoring**: Insights into domain operation performance
6. **Reliability**: Robust database operations supporting business continuity

### Onion Architecture Benefits

1. **Dependency Inversion**: Utilities support infrastructure without affecting higher layers
2. **Technology Encapsulation**: Database-specific optimizations contained in infrastructure
3. **Maintainability**: Centralized database operation utilities
4. **Testability**: Helper utilities can be mocked for testing
5. **Performance**: Optimized database operations without compromising architecture
6. **Scalability**: Efficient database operations supporting application growth

## How to use it

### Installation

```bash
dotnet add package DKNet.EfCore.Relational.Helpers
dotnet add package DKNet.EfCore.Abstractions
```

### Basic Usage Examples

#### 1. DbContext Helper Operations

```csharp
using DKNet.EfCore.Relational.Helpers;

public class CustomerService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CustomerService> _logger;
    
    public CustomerService(ApplicationDbContext context, ILogger<CustomerService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    // Efficient bulk customer creation
    public async Task<int> CreateCustomersInBulkAsync(IEnumerable<CreateCustomerRequest> requests)
    {
        var customers = requests.Select(r => new Customer(r.FirstName, r.LastName, r.Email)).ToList();
        
        // Use helper for efficient bulk insert
        var insertedCount = await DbContextHelpers.BulkInsertAsync(_context, customers);
        
        _logger.LogInformation("Successfully inserted {Count} customers", insertedCount);
        return insertedCount;
    }
    
    // Optimized data retrieval with helper methods
    public async Task<PagedResult<CustomerDto>> GetCustomersPagedAsync(int page, int pageSize, string searchTerm = null)
    {
        var query = _context.Customers.AsQueryable();
        
        // Apply search filter if provided
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(c => c.FirstName.Contains(searchTerm) || 
                                   c.LastName.Contains(searchTerm) || 
                                   c.Email.Contains(searchTerm));
        }
        
        // Use helper for efficient pagination
        return await DbContextHelpers.GetPagedResultAsync(
            query,
            page,
            pageSize,
            customer => new CustomerDto
            {
                Id = customer.Id,
                FullName = $"{customer.FirstName} {customer.LastName}",
                Email = customer.Email,
                IsActive = customer.IsActive
            });
    }
    
    // Transaction management with helpers
    public async Task<Result> ProcessCustomerOrderAsync(int customerId, CreateOrderRequest orderRequest)
    {
        return await DbContextHelpers.ExecuteInTransactionAsync(_context, async () =>
        {
            // Validate customer exists
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
                return Result.Failure("Customer not found");
            
            // Create order
            var order = new Order(customerId, orderRequest.Items);
            _context.Orders.Add(order);
            
            // Update customer statistics
            customer.IncrementOrderCount();
            
            // Save all changes in transaction
            await _context.SaveChangesAsync();
            
            return Result.Success();
        });
    }
}
```

#### 2. Connection Management Utilities

```csharp
public class DatabaseConnectionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseConnectionService> _logger;
    
    public DatabaseConnectionService(IConfiguration configuration, ILogger<DatabaseConnectionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }
    
    // Test database connectivity
    public async Task<bool> TestConnectionAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        
        return await DbContextHelpers.TestConnectionAsync(connectionString, _logger);
    }
    
    // Get database information
    public async Task<DatabaseInfo> GetDatabaseInfoAsync()
    {
        using var context = CreateDbContext();
        
        return await DbContextHelpers.GetDatabaseInfoAsync(context);
    }
    
    // Warm up connection pools
    public async Task WarmUpConnectionsAsync()
    {
        using var context = CreateDbContext();
        
        await DbContextHelpers.WarmUpConnectionPoolAsync(context);
        _logger.LogInformation("Database connection pool warmed up");
    }
    
    private ApplicationDbContext CreateDbContext()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
            
        return new ApplicationDbContext(options);
    }
}

// Database information model
public class DatabaseInfo
{
    public string ServerVersion { get; set; }
    public string DatabaseName { get; set; }
    public int ActiveConnections { get; set; }
    public long DatabaseSize { get; set; }
    public DateTime LastBackup { get; set; }
}
```

#### 3. Bulk Operations Helper

```csharp
public class InventoryService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InventoryService> _logger;
    
    public InventoryService(ApplicationDbContext context, ILogger<InventoryService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    // Bulk inventory update
    public async Task<int> UpdateInventoryLevelsAsync(IEnumerable<InventoryUpdate> updates)
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Use helper for efficient bulk update
        var updatedCount = await DbContextHelpers.BulkUpdateAsync(
            _context,
            updates,
            update => update.ProductId,
            (product, update) =>
            {
                product.StockQuantity = update.NewQuantity;
                product.LastUpdated = DateTime.UtcNow;
            });
        
        stopwatch.Stop();
        _logger.LogInformation("Updated {Count} inventory records in {ElapsedMs}ms", 
            updatedCount, stopwatch.ElapsedMilliseconds);
        
        return updatedCount;
    }
    
    // Bulk delete discontinued products
    public async Task<int> RemoveDiscontinuedProductsAsync(IEnumerable<int> productIds)
    {
        return await DbContextHelpers.BulkDeleteAsync<Product>(
            _context,
            p => productIds.Contains(p.Id));
    }
    
    // Bulk upsert (insert or update)
    public async Task<int> UpsertProductsAsync(IEnumerable<ProductData> productData)
    {
        return await DbContextHelpers.BulkUpsertAsync(
            _context,
            productData,
            data => data.Sku, // Key selector
            data => new Product(data.Name, data.Description, data.Price, data.Category), // Create new
            (product, data) => // Update existing
            {
                product.Name = data.Name;
                product.Description = data.Description;
                product.Price = data.Price;
                product.Category = data.Category;
            });
    }
}

public class InventoryUpdate
{
    public int ProductId { get; set; }
    public int NewQuantity { get; set; }
}

public class ProductData
{
    public string Sku { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public ProductCategory Category { get; set; }
}
```

#### 4. Query Optimization Helpers

```csharp
public class ReportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReportService> _logger;
    
    public ReportService(ApplicationDbContext context, ILogger<ReportService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    // Optimized complex reporting query
    public async Task<SalesReport> GenerateSalesReportAsync(DateTime startDate, DateTime endDate)
    {
        // Use helper for query optimization
        var optimizedQuery = DbContextHelpers.OptimizeQuery(
            _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate));
        
        var salesData = await optimizedQuery
            .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
            .Select(g => new MonthlySales
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalOrders = g.Count(),
                TotalRevenue = g.Sum(o => o.TotalAmount),
                AverageOrderValue = g.Average(o => o.TotalAmount)
            })
            .ToListAsync();
        
        return new SalesReport
        {
            Period = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
            MonthlySales = salesData,
            TotalRevenue = salesData.Sum(s => s.TotalRevenue),
            TotalOrders = salesData.Sum(s => s.TotalOrders)
        };
    }
    
    // Parallel query execution for large datasets
    public async Task<CustomerAnalytics> GetCustomerAnalyticsAsync()
    {
        var tasks = new[]
        {
            GetTopCustomersByRevenueAsync(),
            GetCustomerRetentionStatsAsync(),
            GetCustomerGeographyStatsAsync()
        };
        
        // Use helper for parallel execution
        var results = await DbContextHelpers.ExecuteInParallelAsync(tasks);
        
        return new CustomerAnalytics
        {
            TopCustomers = results[0] as List<TopCustomer>,
            RetentionStats = results[1] as RetentionStats,
            GeographyStats = results[2] as GeographyStats
        };
    }
    
    private async Task<List<TopCustomer>> GetTopCustomersByRevenueAsync()
    {
        return await _context.Orders
            .GroupBy(o => o.CustomerId)
            .Select(g => new TopCustomer
            {
                CustomerId = g.Key,
                CustomerName = g.First().Customer.FirstName + " " + g.First().Customer.LastName,
                TotalRevenue = g.Sum(o => o.TotalAmount),
                OrderCount = g.Count()
            })
            .OrderByDescending(tc => tc.TotalRevenue)
            .Take(10)
            .ToListAsync();
    }
}
```

#### 5. Schema Management Utilities

```csharp
public class DatabaseMaintenanceService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseMaintenanceService> _logger;
    
    public DatabaseMaintenanceService(ApplicationDbContext context, ILogger<DatabaseMaintenanceService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    // Check database schema consistency
    public async Task<SchemaValidationResult> ValidateSchemaAsync()
    {
        return await DbContextHelpers.ValidateSchemaAsync(_context);
    }
    
    // Optimize database performance
    public async Task OptimizeDatabaseAsync()
    {
        var optimizationResults = await DbContextHelpers.OptimizeDatabaseAsync(_context, new DatabaseOptimizationOptions
        {
            RebuildIndexes = true,
            UpdateStatistics = true,
            ShrinkDatabase = false,
            AnalyzeQueryPlans = true
        });
        
        _logger.LogInformation("Database optimization completed: {Results}", optimizationResults);
    }
    
    // Get table statistics
    public async Task<IEnumerable<TableStatistics>> GetTableStatisticsAsync()
    {
        return await DbContextHelpers.GetTableStatisticsAsync(_context);
    }
    
    // Backup database
    public async Task<bool> BackupDatabaseAsync(string backupPath)
    {
        try
        {
            await DbContextHelpers.BackupDatabaseAsync(_context, backupPath);
            _logger.LogInformation("Database backup completed successfully to {BackupPath}", backupPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database backup failed");
            return false;
        }
    }
}

public class SchemaValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class TableStatistics
{
    public string TableName { get; set; }
    public long RowCount { get; set; }
    public long DataSize { get; set; }
    public long IndexSize { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

#### 6. Performance Monitoring

```csharp
public class DatabasePerformanceMonitor
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabasePerformanceMonitor> _logger;
    private readonly IMetricsCollector _metricsCollector;
    
    public DatabasePerformanceMonitor(
        ApplicationDbContext context, 
        ILogger<DatabasePerformanceMonitor> logger,
        IMetricsCollector metricsCollector)
    {
        _context = context;
        _logger = logger;
        _metricsCollector = metricsCollector;
    }
    
    // Monitor query performance
    public async Task<QueryPerformanceReport> MonitorQueryPerformanceAsync(TimeSpan duration)
    {
        var performanceData = new List<QueryMetrics>();
        var stopwatch = Stopwatch.StartNew();
        
        // Use helper to monitor queries
        using var monitor = DbContextHelpers.CreateQueryMonitor(_context);
        
        monitor.QueryExecuted += (sender, args) =>
        {
            performanceData.Add(new QueryMetrics
            {
                Query = args.Query,
                ExecutionTime = args.Duration,
                RecordsAffected = args.RecordsAffected,
                Timestamp = DateTime.UtcNow
            });
        };
        
        // Wait for monitoring duration
        await Task.Delay(duration);
        
        return new QueryPerformanceReport
        {
            MonitoringDuration = duration,
            TotalQueries = performanceData.Count,
            AverageExecutionTime = performanceData.Average(q => q.ExecutionTime.TotalMilliseconds),
            SlowestQueries = performanceData.OrderByDescending(q => q.ExecutionTime).Take(10).ToList(),
            FastestQueries = performanceData.OrderBy(q => q.ExecutionTime).Take(10).ToList()
        };
    }
    
    // Database health check
    public async Task<DatabaseHealthStatus> CheckDatabaseHealthAsync()
    {
        var healthChecks = new List<HealthCheckResult>();
        
        // Connection health
        var connectionHealth = await DbContextHelpers.CheckConnectionHealthAsync(_context);
        healthChecks.Add(new HealthCheckResult("Connection", connectionHealth.IsHealthy, connectionHealth.Message));
        
        // Performance health
        var performanceHealth = await DbContextHelpers.CheckPerformanceHealthAsync(_context);
        healthChecks.Add(new HealthCheckResult("Performance", performanceHealth.IsHealthy, performanceHealth.Message));
        
        // Storage health
        var storageHealth = await DbContextHelpers.CheckStorageHealthAsync(_context);
        healthChecks.Add(new HealthCheckResult("Storage", storageHealth.IsHealthy, storageHealth.Message));
        
        var overallHealth = healthChecks.All(h => h.IsHealthy);
        
        return new DatabaseHealthStatus
        {
            IsHealthy = overallHealth,
            HealthChecks = healthChecks,
            CheckedAt = DateTime.UtcNow
        };
    }
}

public class QueryMetrics
{
    public string Query { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public int RecordsAffected { get; set; }
    public DateTime Timestamp { get; set; }
}

public class DatabaseHealthStatus
{
    public bool IsHealthy { get; set; }
    public List<HealthCheckResult> HealthChecks { get; set; }
    public DateTime CheckedAt { get; set; }
}

public class HealthCheckResult
{
    public string Name { get; set; }
    public bool IsHealthy { get; set; }
    public string Message { get; set; }
    
    public HealthCheckResult(string name, bool isHealthy, string message)
    {
        Name = name;
        IsHealthy = isHealthy;
        Message = message;
    }
}
```

### Advanced Usage Examples

#### 1. Custom Database Provider Helpers

```csharp
// SQL Server specific helpers
public static class SqlServerHelpers
{
    public static async Task<bool> EnableSnapshotIsolationAsync(DbContext context)
    {
        return await DbContextHelpers.ExecuteRawSqlAsync(context,
            "ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON");
    }
    
    public static async Task<List<IndexRecommendation>> GetIndexRecommendationsAsync(DbContext context)
    {
        return await DbContextHelpers.ExecuteQueryAsync<IndexRecommendation>(context,
            @"SELECT 
                user_seeks * avg_total_user_cost * (avg_user_impact * 0.01) AS improvement_measure,
                'CREATE INDEX [missing_index_' + CONVERT(varchar, mig.index_group_handle) + '_' + CONVERT(varchar, mid.index_handle) + ']'
                + ' ON ' + statement + ' (' + ISNULL(equality_columns,'') + CASE WHEN equality_columns IS NOT NULL AND inequality_columns IS NOT NULL THEN ',' ELSE '' END + ISNULL(inequality_columns, '') + ')' 
                + ISNULL(' INCLUDE (' + included_columns + ')', '') AS create_index_statement
              FROM sys.dm_db_missing_index_groups mig
              INNER JOIN sys.dm_db_missing_index_group_stats migs ON migs.group_handle = mig.index_group_handle
              INNER JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
              WHERE migs.user_seeks > 100
              ORDER BY improvement_measure DESC");
    }
}

// PostgreSQL specific helpers
public static class PostgreSqlHelpers
{
    public static async Task<bool> EnableLoggingAsync(DbContext context)
    {
        return await DbContextHelpers.ExecuteRawSqlAsync(context,
            "SET log_statement = 'all'");
    }
    
    public static async Task VacuumTableAsync(DbContext context, string tableName)
    {
        await DbContextHelpers.ExecuteRawSqlAsync(context,
            $"VACUUM ANALYZE {tableName}");
    }
}
```

#### 2. Migration Helpers

```csharp
public class MigrationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MigrationService> _logger;
    
    public async Task<bool> ApplyPendingMigrationsAsync()
    {
        try
        {
            var pendingMigrations = await DbContextHelpers.GetPendingMigrationsAsync(_context);
            
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
                
                await DbContextHelpers.MigrateAsync(_context);
                
                _logger.LogInformation("All migrations applied successfully");
                return true;
            }
            
            _logger.LogInformation("No pending migrations found");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply migrations");
            return false;
        }
    }
    
    public async Task<MigrationStatus> GetMigrationStatusAsync()
    {
        var appliedMigrations = await DbContextHelpers.GetAppliedMigrationsAsync(_context);
        var pendingMigrations = await DbContextHelpers.GetPendingMigrationsAsync(_context);
        
        return new MigrationStatus
        {
            AppliedMigrations = appliedMigrations.ToList(),
            PendingMigrations = pendingMigrations.ToList(),
            IsUpToDate = !pendingMigrations.Any()
        };
    }
}
```

## Best Practices

### 1. Helper Usage Patterns

```csharp
// Good: Use helpers for infrastructure concerns
public class CustomerRepository
{
    public async Task<int> BulkCreateCustomersAsync(IEnumerable<Customer> customers)
    {
        return await DbContextHelpers.BulkInsertAsync(_context, customers);
    }
}

// Avoid: Using helpers in domain logic
public class Customer
{
    public async Task SaveAsync(DbContext context)
    {
        // Don't: Domain entities shouldn't use infrastructure helpers
        await DbContextHelpers.BulkInsertAsync(context, new[] { this });
    }
}
```

### 2. Performance Monitoring

```csharp
// Good: Monitor critical operations
public async Task<Result> ProcessLargeDataSet(IEnumerable<DataItem> items)
{
    using var monitor = DbContextHelpers.CreatePerformanceMonitor(_context);
    
    var result = await DbContextHelpers.BulkProcessAsync(_context, items, ProcessItem);
    
    if (monitor.ElapsedTime > TimeSpan.FromMinutes(5))
    {
        _logger.LogWarning("Long-running operation detected: {ElapsedMs}ms", 
            monitor.ElapsedTime.TotalMilliseconds);
    }
    
    return result;
}
```

### 3. Error Handling

```csharp
// Good: Robust error handling with helpers
public async Task<Result> ProcessBulkOperationAsync(IEnumerable<DataItem> items)
{
    try
    {
        return await DbContextHelpers.ExecuteWithRetryAsync(_context, async () =>
        {
            await DbContextHelpers.BulkProcessAsync(_context, items, ProcessItem);
            return Result.Success();
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Bulk operation failed");
        return Result.Failure(ex.Message);
    }
}
```

## Integration with Other DKNet Components

DKNet.EfCore.Relational.Helpers integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Abstractions**: Uses entity base classes and interfaces for operations
- **DKNet.EfCore.Repos**: Enhances repository operations with helper utilities
- **DKNet.EfCore.Events**: Supports efficient event processing with bulk operations
- **DKNet.EfCore.Hooks**: Provides performance monitoring for hook operations
- **DKNet.Fw.Extensions**: Leverages core framework utilities for common operations

---

> 💡 **Performance Tip**: Use DKNet.EfCore.Relational.Helpers to optimize your database operations while maintaining clean architecture. The helpers provide database-specific optimizations and utilities that can significantly improve performance for bulk operations and complex queries. Always monitor the performance impact of helper usage and choose the right helper methods for your specific use cases.