using DKNet.EfCore.Specifications.Extensions;

namespace EfCore.Specifications.Tests.Definitions;

public class SpecificationIncludeTests(TestDbFixture fixture) : IClassFixture<TestDbFixture>
{
    private readonly TestDbContext _context = fixture.Db!;
    private readonly IRepositorySpec _repository = new RepositorySpec<TestDbContext>(fixture.Db!, (IServiceProvider?)null);

    [Fact]
    public void FilteredInclude_SingleLevel_FilteredNavAppearsInSql()
    {
        var spec = new ProductWithFilteredOrderItemsSpec(minQuantity: 0);

        var sql = _repository.Query(spec).ToQueryString();

        var ns = NormalizeSql(sql);
        ns.ShouldContain("JOIN", Case.Insensitive);
        ns.ShouldContain("OrderItems", Case.Insensitive);
        ns.ShouldContain(".Quantity", Case.Insensitive);
        ns.ShouldContain(">", Case.Insensitive);
    }

    [Fact]
    public void FilteredInclude_SingleLevel_OnlyMatchingItemsReturned()
    {
        var product = _context.Products.First();
        _context.OrderItems.Add(new OrderItem { OrderId = _context.Orders.First().Id, ProductId = product.Id, Quantity = 0, UnitPrice = 1 });
        _context.OrderItems.Add(new OrderItem { OrderId = _context.Orders.First().Id, ProductId = product.Id, Quantity = 5, UnitPrice = 1 });
        _context.SaveChanges();

        var spec = new ProductWithFilteredOrderItemsSpec(minQuantity: 3);

        var products = _context.Products.ApplySpecs(spec).AsNoTracking().ToList();

        products.ShouldNotBeEmpty();
        foreach (var p in products)
            if (p.OrderItems.Count > 0)
                p.OrderItems.ShouldAllBe(i => i.Quantity >= 3);
    }

    [Fact]
    public void FilteredInclude_ThenInclude_FromOrder_JoinsBothLevels()
    {
        var spec = new OrderWithItemsAndProductSpec();

        var sql = _repository.Query(spec).ToQueryString();

        var ns = NormalizeSql(sql);
        ns.ShouldContain("OrderItems", Case.Insensitive);
        ns.ShouldContain("Product", Case.Insensitive);
        ns.ShouldContain("JOIN", Case.Insensitive);
    }

    [Fact]
    public void FilteredInclude_ThenInclude_FromOrder_MaterializesNestedNav()
    {
        var spec = new OrderWithItemsAndProductSpec();

        var orders = _context.Orders.ApplySpecs(spec).AsNoTracking().ToList();

        orders.ShouldNotBeEmpty();
        foreach (var o in orders.Where(o => o.OrderItems.Count > 0))
            o.OrderItems.ShouldAllBe(i => i.Product != null);
    }

    [Fact]
    public void FilteredInclude_FilterAtDeeperLevel_ThenInclude_FilterAppearsInSql()
    {
        var spec = new OrderWithFilteredItemsAndProductSpec(minQuantity: 2);

        var sql = _repository.Query(spec).ToQueryString();

        var ns = NormalizeSql(sql);
        ns.ShouldContain("OrderItems", Case.Insensitive);
        ns.ShouldContain("Product", Case.Insensitive);
        ns.ShouldContain(".Quantity", Case.Insensitive);
        ns.ShouldContain(">", Case.Insensitive);
    }

    [Fact]
    public void FilteredInclude_FilterAtDeeperLevel_OnlyMatchingItemsReturned()
    {
        var order = _context.Orders.First();
        _context.OrderItems.Add(new OrderItem { OrderId = order.Id, ProductId = _context.Products.First().Id, Quantity = 1, UnitPrice = 1 });
        _context.OrderItems.Add(new OrderItem { OrderId = order.Id, ProductId = _context.Products.Skip(1).First().Id, Quantity = 5, UnitPrice = 1 });
        _context.SaveChanges();

        var spec = new OrderWithFilteredItemsAndProductSpec(minQuantity: 3);

        var orders = _context.Orders.ApplySpecs(spec).AsNoTracking().ToList();

        orders.ShouldNotBeEmpty();
        foreach (var o in orders.Where(o => o.OrderItems.Count > 0))
            o.OrderItems.ShouldAllBe(i => i.Quantity >= 3);
    }

    [Fact]
    public void ExpressionInclude_Unchanged_StillGeneratesJoin()
    {
        var spec = new ProductWithCategoryIncludeSpec();

        var sql = _repository.Query(spec).ToQueryString();

        var ns = NormalizeSql(sql);
        ns.ShouldContain(".Category", Case.Insensitive);
        ns.ShouldContain("JOIN", Case.Insensitive);
    }

    [Fact]
    public void ExpressionInclude_Unchanged_MaterializesNavigation()
    {
        var spec = new ProductWithCategoryIncludeSpec();

        var result = _context.Products.ApplySpecs(spec).AsNoTracking().First();

        result.Category.ShouldNotBeNull();
        result.Category.Name.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void CopyConstructor_CarriesOverExpressionInclude()
    {
        var original = new ProductWithIncludesSpec();
        var copy = new CopyProductSpec(original);

        copy.IncludeQueries.Count.ShouldBe(original.IncludeQueries.Count);
        copy.IncludeQueries.ShouldAllBe(q => original.IncludeQueries.Contains(q));
    }

    [Fact]
    public void CopyConstructor_CarriesOverBuilderInclude()
    {
        var original = new OrderWithItemsAndProductSpec();
        var copy = new CopyOrderSpec(original);

        copy.IncludeBuilders.Count.ShouldBe(original.IncludeBuilders.Count);
    }

    [Fact]
    public void CopyConstructor_CarriesOverBothIncludeTypes()
    {
        var original = new ProductWithMixedIncludesSpec();
        var copy = new CopyProductSpec(original);

        copy.IncludeQueries.Count.ShouldBe(1);
        copy.IncludeBuilders.Count.ShouldBe(1);
    }

    [Fact]
    public void CopyConstructor_IncludeBuildersAreIndependent()
    {
        var original = new OrderWithItemsAndProductSpec();
        var copy = new CopyOrderSpec(original);
        var secondCopy = new CopyOrderSpec(copy);

        secondCopy.IncludeBuilders.Count.ShouldBe(original.IncludeBuilders.Count);
    }

    private static string NormalizeSql(string sql) =>
        sql.Replace("\"", "", StringComparison.Ordinal)
           .Replace("[", "", StringComparison.Ordinal)
           .Replace("]", "", StringComparison.Ordinal);

    private sealed class ProductWithFilteredOrderItemsSpec : Specification<Product>
    {
        public ProductWithFilteredOrderItemsSpec(int minQuantity)
        {
            AddInclude(p => p.OrderItems.Where(i => i.Quantity > minQuantity));
        }
    }

    private sealed class OrderWithItemsAndProductSpec : Specification<Order>
    {
        public OrderWithItemsAndProductSpec()
        {
            AddInclude(q => q.Include(o => o.OrderItems).ThenInclude(i => i.Product));
        }
    }

    private sealed class OrderWithFilteredItemsAndProductSpec : Specification<Order>
    {
        public OrderWithFilteredItemsAndProductSpec(int minQuantity)
        {
            AddInclude(q => q.Include(o => o.OrderItems.Where(i => i.Quantity > minQuantity)).ThenInclude(i => i.Product));
        }
    }

    private sealed class ProductWithCategoryIncludeSpec : Specification<Product>
    {
        public ProductWithCategoryIncludeSpec()
        {
            AddInclude(p => p.Category);
        }
    }

    private sealed class ProductWithIncludesSpec : Specification<Product>
    {
        public ProductWithIncludesSpec()
        {
            AddInclude(p => p.Category);
            AddInclude(p => p.OrderItems);
        }
    }

    private sealed class ProductWithMixedIncludesSpec : Specification<Product>
    {
        public ProductWithMixedIncludesSpec()
        {
            AddInclude(p => p.Category);
            AddInclude(q => q.Include(p => p.Category));
        }
    }

    private sealed class CopyProductSpec : Specification<Product>
    {
        public CopyProductSpec(ISpecification<Product> source) : base(source) { }
    }

    private sealed class CopyOrderSpec : Specification<Order>
    {
        public CopyOrderSpec(ISpecification<Order> source) : base(source) { }
    }
}