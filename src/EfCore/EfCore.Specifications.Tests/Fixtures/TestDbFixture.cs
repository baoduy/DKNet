using Bogus;
using Testcontainers.MsSql;

namespace EfCore.Specifications.Tests.Fixtures;

public class TestDbFixture : IAsyncLifetime
{
    #region Fields

    private readonly Faker<Category> _categoryFaker;
    private readonly Faker<Order> _orderFaker;
    private readonly Faker<Product> _productFaker;
    private MsSqlContainer? _msSqlContainer;

    #endregion

    #region Constructors

    public TestDbFixture()
    {
        _categoryFaker = new Faker<Category>()
            .RuleFor(c => c.Name, f => f.Commerce.Categories(1)[0])
            .RuleFor(c => c.Description, f => f.Lorem.Sentence());

        _productFaker = new Faker<Product>()
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
            .RuleFor(p => p.Price, f => f.Random.Decimal(1, 1000))
            .RuleFor(p => p.StockQuantity, f => f.Random.Int(0, 100))
            .RuleFor(p => p.IsActive, f => f.Random.Bool(0.8f))
            .RuleFor(p => p.CreatedDate, f => f.Date.Past());

        _orderFaker = new Faker<Order>()
            .RuleFor(o => o.OrderDate, f => f.Date.Past())
            .RuleFor(o => o.CustomerName, f => f.Name.FullName())
            .RuleFor(o => o.CustomerEmail, f => f.Internet.Email())
            .RuleFor(o => o.Status, f => f.PickRandom<OrderStatus>())
            .RuleFor(o => o.TotalAmount, f => f.Random.Decimal(10, 5000));
    }

    #endregion

    #region Properties

    public TestDbContext? Db { get; private set; }

    #endregion

    #region Methods

    public async Task DisposeAsync()
    {
        if (Db != null) await Db.DisposeAsync();

        if (_msSqlContainer != null) await _msSqlContainer.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        // Start SQL Server container
        _msSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong@Passw0rd")
            .WithCleanUp(true)
            .Build();

        await _msSqlContainer.StartAsync();

        // Create DbContext with SQL Server connection
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .LogTo(Console.WriteLine, LogLevel.Information)
            .Options;

        Db = new TestDbContext(options);

        // Create database schema
        await Db.Database.EnsureCreatedAsync();

        // Seed data
        await SeedDataAsync();
    }

    private async Task SeedDataAsync()
    {
        if (Db == null) return;

        // Create categories
        var categories = _categoryFaker.Generate(5);
        await Db.Categories.AddRangeAsync(categories);
        await Db.SaveChangesAsync();

        // Create products
        var products = _productFaker.Generate(20);
        for (var i = 0; i < products.Count; i++) products[i].CategoryId = categories[i % categories.Count].Id;
        await Db.Products.AddRangeAsync(products);
        await Db.SaveChangesAsync();

        // Create product tags
        var tags = new[] { "New", "Featured", "Sale", "Premium", "Limited" };
        foreach (var product in products.Take(10))
        {
            var productTag = new ProductTag
            {
                ProductId = product.Id,
                Tag = tags[product.Id % tags.Length]
            };
            await Db.ProductTags.AddAsync(productTag);
        }

        await Db.SaveChangesAsync();

        // Create orders
        var orders = _orderFaker.Generate(15);
        await Db.Orders.AddRangeAsync(orders);
        await Db.SaveChangesAsync();

        // Create order items
        foreach (var order in orders)
        {
            var orderItemCount = new Random(order.Id).Next(1, 4);
            for (var i = 0; i < orderItemCount; i++)
            {
                var product = products[new Random(order.Id + i).Next(products.Count)];
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = new Random(order.Id + i).Next(1, 5),
                    UnitPrice = product.Price
                };
                await Db.OrderItems.AddAsync(orderItem);
            }
        }

        await Db.SaveChangesAsync();
    }

    #endregion
}