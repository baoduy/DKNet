# DKNet.Svc.Transformation

**Data transformation services that provide powerful token extraction, resolution, and formatting capabilities for dynamic data processing, template processing, and content transformation while supporting Domain-Driven Design (DDD) and Onion Architecture principles.**

## What is this project?

DKNet.Svc.Transformation provides a comprehensive transformation engine for processing dynamic content, extracting and resolving tokens, and performing data format conversions. It enables applications to process templates, transform data between different formats, and implement complex data processing pipelines with a focus on extensibility and type safety.

### Key Features

- **ITransformerService**: Core transformation service interface and implementation
- **Token Extraction**: Pattern-based token discovery and extraction from content
- **Token Resolution**: Dynamic token resolution from various data sources
- **Format Converters**: Currency, date-time, boolean, and custom format converters
- **Pipeline Processing**: Chainable transformation operations
- **Template Processing**: Dynamic template rendering with token substitution
- **Type Safety**: Strongly-typed transformation with comprehensive error handling
- **Extensibility**: Plugin architecture for custom converters and resolvers
- **Performance Optimization**: Efficient processing with caching and optimization
- **Validation**: Comprehensive input validation and error reporting

## How it contributes to DDD and Onion Architecture

### Application Layer Service

DKNet.Svc.Transformation implements **Application Layer services** that orchestrate data transformation operations without containing business logic, supporting the Onion Architecture:

```
┌─────────────────────────────────────────────────────────────────┐
│                    🌐 Presentation Layer                        │
│                   (Controllers, API Endpoints)                  │
│                                                                 │
│  Uses: Template rendering, data formatting endpoints           │
│  Returns: Processed content, formatted data, transformation    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   🎯 Application Layer                          │
│              (Use Cases, Application Services)                  │
│                                                                 │
│  🔧 ITransformerService - Data transformation orchestration    │
│  📊 Template processing workflows                              │
│  💱 Currency and format conversions                            │
│  🔄 Data pipeline coordination                                 │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    💼 Domain Layer                             │
│           (Entities, Aggregates, Domain Services)              │
│                                                                 │
│  🎭 Domain entities provide data for transformation            │
│  📝 Business rules define transformation requirements          │
│  🏷️ Domain remains unaware of transformation implementation    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                 🗄️ Infrastructure Layer                        │
│                  (Transformation Engine, Converters)           │
│                                                                 │
│  ⚙️ TransformerService - Core transformation implementation    │
│  📈 Format converters (currency, date, boolean)               │
│  🔍 Token extractors and resolvers                            │
│  📊 Template engines and processing pipelines                 │
│  🎯 Custom converter implementations                           │
└─────────────────────────────────────────────────────────────────┘
```

### DDD Benefits

1. **Business Rule Expression**: Transformations can express business formatting rules
2. **Domain Language**: Token names and formats use ubiquitous language
3. **Value Object Support**: Transformation of domain value objects to display formats
4. **Aggregate Integration**: Process data from multiple aggregates in templates
5. **Event Processing**: Transform domain events for external systems
6. **Reporting**: Generate business reports with domain-specific formatting

### Onion Architecture Benefits

1. **Dependency Inversion**: Application layer defines transformation contracts
2. **Separation of Concerns**: Transformation logic isolated from business logic
3. **Testability**: Transformation services can be easily mocked and tested
4. **Technology Independence**: Abstract transformation patterns independent of implementation
5. **Extensibility**: Easy to add new transformations without affecting core business logic
6. **Performance**: Optimized transformation pipelines without impacting domain operations

## How to use it

### Installation

```bash
dotnet add package DKNet.Svc.Transformation
dotnet add package DKNet.Fw.Extensions
```

### Basic Usage Examples

#### 1. Configuration and Setup

```csharp
using DKNet.Svc.Transformation;

// appsettings.json configuration
{
  "TransformationService": {
    "DefaultCulture": "en-US",
    "CacheTokenResults": true,
    "CacheExpirationMinutes": 30,
    "MaxTokenDepth": 10,
    "DefaultDateTimeFormat": "yyyy-MM-dd HH:mm:ss",
    "DefaultCurrencyFormat": "C2",
    "EnableCustomConverters": true,
    "ValidationEnabled": true
  }
}

// Service registration
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransformationServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure transformation options
        services.Configure<TransformOptions>(configuration.GetSection("TransformationService"));
        
        // Register transformation service
        services.AddScoped<ITransformerService, TransformerService>();
        
        // Register built-in converters
        services.AddScoped<ITokenExtractor, TokenExtractor>();
        services.AddScoped<ITokenResolver, TokenResolver>();
        services.AddScoped<IValueFormatter, ValueFormatter>();
        services.AddScoped<ICurrencyConverter, CurrencyConverter>();
        
        // Register custom converters
        services.AddScoped<ICustomConverter, DateTimeConverter>();
        services.AddScoped<ICustomConverter, BooleanConverter>();
        services.AddScoped<ICustomConverter, NumberConverter>();
        
        return services;
    }
}

// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddTransformationServices(Configuration);
}
```

#### 2. Basic Transformation Operations

```csharp
public class DocumentService
{
    private readonly ITransformerService _transformerService;
    private readonly ILogger<DocumentService> _logger;
    
    public DocumentService(ITransformerService transformerService, ILogger<DocumentService> logger)
    {
        _transformerService = transformerService;
        _logger = logger;
    }
    
    // Transform document template with customer data
    public async Task<string> GenerateCustomerDocumentAsync(string templateContent, Customer customer)
    {
        try
        {
            var dataContext = new Dictionary<string, object>
            {
                ["customer.id"] = customer.Id,
                ["customer.name"] = $"{customer.FirstName} {customer.LastName}",
                ["customer.email"] = customer.Email,
                ["customer.phone"] = customer.PhoneNumber,
                ["customer.address"] = customer.Address?.ToString(),
                ["customer.joinDate"] = customer.JoinDate,
                ["customer.isActive"] = customer.IsActive,
                ["customer.totalOrders"] = customer.TotalOrders,
                ["customer.totalSpent"] = customer.TotalSpent
            };
            
            var transformedContent = await _transformerService.TransformAsync(templateContent, dataContext);
            
            _logger.LogInformation("Document transformed successfully for customer: {CustomerId}", customer.Id);
            return transformedContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transform document for customer: {CustomerId}", customer.Id);
            throw;
        }
    }
    
    // Transform invoice template with order data
    public async Task<string> GenerateInvoiceAsync(string invoiceTemplate, Order order)
    {
        var dataContext = new Dictionary<string, object>
        {
            ["invoice.number"] = order.InvoiceNumber,
            ["invoice.date"] = order.OrderDate,
            ["invoice.dueDate"] = order.OrderDate.AddDays(30),
            ["customer.name"] = order.Customer.FullName,
            ["customer.email"] = order.Customer.Email,
            ["customer.address"] = order.Customer.BillingAddress,
            ["order.id"] = order.Id,
            ["order.total"] = order.TotalAmount,
            ["order.tax"] = order.TaxAmount,
            ["order.subtotal"] = order.SubtotalAmount,
            ["order.items"] = order.OrderItems.Select(item => new
            {
                name = item.Product.Name,
                quantity = item.Quantity,
                price = item.UnitPrice,
                total = item.TotalPrice
            }).ToList(),
            ["company.name"] = "Acme Corporation",
            ["company.address"] = "123 Business St, City, State 12345",
            ["company.phone"] = "(555) 123-4567"
        };
        
        return await _transformerService.TransformAsync(invoiceTemplate, dataContext);
    }
    
    // Format currency values for different locales
    public async Task<string> FormatCurrencyAsync(decimal amount, string currencyCode, string culture = "en-US")
    {
        var template = "{amount:currency:" + currencyCode + "}";
        var dataContext = new Dictionary<string, object>
        {
            ["amount"] = amount
        };
        
        var transformOptions = new TransformationOptions
        {
            Culture = new CultureInfo(culture)
        };
        
        return await _transformerService.TransformAsync(template, dataContext, transformOptions);
    }
    
    // Process email template with dynamic content
    public async Task<EmailContent> ProcessEmailTemplateAsync(EmailTemplate template, Dictionary<string, object> data)
    {
        var subjectTask = _transformerService.TransformAsync(template.Subject, data);
        var bodyTask = _transformerService.TransformAsync(template.Body, data);
        
        var results = await Task.WhenAll(subjectTask, bodyTask);
        
        return new EmailContent
        {
            Subject = results[0],
            Body = results[1],
            IsHtml = template.IsHtml
        };
    }
}

public class EmailContent
{
    public string Subject { get; set; }
    public string Body { get; set; }
    public bool IsHtml { get; set; }
}
```

#### 3. Advanced Token Processing

```csharp
public class AdvancedTransformationService
{
    private readonly ITransformerService _transformerService;
    private readonly ITokenExtractor _tokenExtractor;
    private readonly ITokenResolver _tokenResolver;
    private readonly ILogger<AdvancedTransformationService> _logger;
    
    public AdvancedTransformationService(
        ITransformerService transformerService,
        ITokenExtractor tokenExtractor,
        ITokenResolver tokenResolver,
        ILogger<AdvancedTransformationService> logger)
    {
        _transformerService = transformerService;
        _tokenExtractor = tokenExtractor;
        _tokenResolver = tokenResolver;
        _logger = logger;
    }
    
    // Extract and analyze tokens from content
    public async Task<TokenAnalysisResult> AnalyzeTokensAsync(string content)
    {
        var tokens = await _tokenExtractor.ExtractTokensAsync(content);
        var analysis = new TokenAnalysisResult
        {
            TotalTokens = tokens.Count(),
            UniqueTokens = tokens.Select(t => t.Name).Distinct().Count(),
            TokensByType = tokens.GroupBy(t => t.Type)
                                .ToDictionary(g => g.Key, g => g.Count()),
            ComplexTokens = tokens.Where(t => t.HasFormatter || t.HasParameters).ToList(),
            SimpleTokens = tokens.Where(t => !t.HasFormatter && !t.HasParameters).ToList()
        };
        
        return analysis;
    }
    
    // Process template with conditional logic
    public async Task<string> ProcessConditionalTemplateAsync(string template, Dictionary<string, object> data)
    {
        // Add conditional processing helpers to data context
        data["if"] = new Func<bool, object, object, object>((condition, trueValue, falseValue) =>
            condition ? trueValue : falseValue);
        
        data["isEmpty"] = new Func<object, bool>(value =>
            value == null || string.IsNullOrWhiteSpace(value.ToString()));
        
        data["isNotEmpty"] = new Func<object, bool>(value =>
            value != null && !string.IsNullOrWhiteSpace(value.ToString()));
        
        data["format"] = new Func<object, string, string>((value, format) =>
        {
            if (value is DateTime dt)
                return dt.ToString(format);
            if (value is decimal dec)
                return dec.ToString(format);
            if (value is double dbl)
                return dbl.ToString(format);
            return value?.ToString() ?? "";
        });
        
        return await _transformerService.TransformAsync(template, data);
    }
    
    // Process template with nested object resolution
    public async Task<string> ProcessNestedObjectTemplateAsync(string template, object dataObject)
    {
        var flattenedData = FlattenObject(dataObject);
        return await _transformerService.TransformAsync(template, flattenedData);
    }
    
    // Batch process multiple templates
    public async Task<BatchTransformationResult> BatchTransformAsync(
        IEnumerable<TemplateProcessingRequest> requests)
    {
        var results = new List<TemplateProcessingResult>();
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        
        var tasks = requests.Select(async request =>
        {
            await semaphore.WaitAsync();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await _transformerService.TransformAsync(request.Template, request.Data);
                stopwatch.Stop();
                
                return new TemplateProcessingResult
                {
                    Id = request.Id,
                    Success = true,
                    Result = result,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process template: {TemplateId}", request.Id);
                return new TemplateProcessingResult
                {
                    Id = request.Id,
                    Success = false,
                    Error = ex.Message
                };
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        var batchResults = await Task.WhenAll(tasks);
        
        return new BatchTransformationResult
        {
            TotalRequests = requests.Count(),
            SuccessfulTransformations = batchResults.Count(r => r.Success),
            FailedTransformations = batchResults.Count(r => !r.Success),
            Results = batchResults.ToList(),
            TotalProcessingTime = batchResults.Where(r => r.Success).Sum(r => r.ProcessingTime?.TotalMilliseconds ?? 0)
        };
    }
    
    // Custom data resolver for complex scenarios
    public async Task<string> ProcessWithCustomResolverAsync(
        string template, 
        Func<string, Task<object>> customResolver)
    {
        var tokens = await _tokenExtractor.ExtractTokensAsync(template);
        var resolvedData = new Dictionary<string, object>();
        
        foreach (var token in tokens.Where(t => !string.IsNullOrEmpty(t.Name)))
        {
            try
            {
                var value = await customResolver(token.Name);
                if (value != null)
                {
                    resolvedData[token.Name] = value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve token: {TokenName}", token.Name);
                resolvedData[token.Name] = $"[Error resolving {token.Name}]";
            }
        }
        
        return await _transformerService.TransformAsync(template, resolvedData);
    }
    
    private Dictionary<string, object> FlattenObject(object obj, string prefix = "")
    {
        var result = new Dictionary<string, object>();
        
        if (obj == null) return result;
        
        var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var property in properties)
        {
            var value = property.GetValue(obj);
            var key = string.IsNullOrEmpty(prefix) ? property.Name.ToLowerInvariant() : $"{prefix}.{property.Name.ToLowerInvariant()}";
            
            if (value == null)
            {
                result[key] = null;
            }
            else if (value.GetType().IsPrimitive || value is string || value is DateTime || value is decimal)
            {
                result[key] = value;
            }
            else if (value is IEnumerable enumerable && !(value is string))
            {
                result[key] = enumerable;
            }
            else
            {
                // Recursively flatten nested objects
                var nestedData = FlattenObject(value, key);
                foreach (var kvp in nestedData)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
        }
        
        return result;
    }
}

public class TokenAnalysisResult
{
    public int TotalTokens { get; set; }
    public int UniqueTokens { get; set; }
    public Dictionary<string, int> TokensByType { get; set; }
    public List<Token> ComplexTokens { get; set; }
    public List<Token> SimpleTokens { get; set; }
}

public class TemplateProcessingRequest
{
    public string Id { get; set; }
    public string Template { get; set; }
    public Dictionary<string, object> Data { get; set; }
}

public class TemplateProcessingResult
{
    public string Id { get; set; }
    public bool Success { get; set; }
    public string Result { get; set; }
    public string Error { get; set; }
    public TimeSpan? ProcessingTime { get; set; }
}

public class BatchTransformationResult
{
    public int TotalRequests { get; set; }
    public int SuccessfulTransformations { get; set; }
    public int FailedTransformations { get; set; }
    public List<TemplateProcessingResult> Results { get; set; }
    public double TotalProcessingTime { get; set; }
}
```

#### 4. Custom Converters and Formatters

```csharp
// Custom converter for business-specific formatting
public class BusinessDataConverter : ICustomConverter
{
    public string Name => "business";
    
    public bool CanConvert(string format, Type valueType)
    {
        return format.StartsWith("business:");
    }
    
    public async Task<string> ConvertAsync(object value, string format, CultureInfo culture)
    {
        var formatParts = format.Split(':');
        if (formatParts.Length < 2) return value?.ToString() ?? "";
        
        var businessFormat = formatParts[1];
        
        return businessFormat switch
        {
            "employeeId" => FormatEmployeeId(value),
            "customerCode" => FormatCustomerCode(value),
            "productSku" => FormatProductSku(value),
            "orderNumber" => FormatOrderNumber(value),
            "accountNumber" => FormatAccountNumber(value),
            _ => value?.ToString() ?? ""
        };
    }
    
    private string FormatEmployeeId(object value)
    {
        if (value is int id)
            return $"EMP-{id:D6}"; // EMP-000123
        return value?.ToString() ?? "";
    }
    
    private string FormatCustomerCode(object value)
    {
        if (value is string code)
            return $"CUST-{code.ToUpperInvariant()}";
        return value?.ToString() ?? "";
    }
    
    private string FormatProductSku(object value)
    {
        if (value is string sku)
            return sku.ToUpperInvariant().Replace(" ", "-");
        return value?.ToString() ?? "";
    }
    
    private string FormatOrderNumber(object value)
    {
        if (value is int orderNumber)
        {
            var year = DateTime.Now.Year;
            return $"ORD-{year}-{orderNumber:D8}"; // ORD-2024-00000123
        }
        return value?.ToString() ?? "";
    }
    
    private string FormatAccountNumber(object value)
    {
        if (value is string accountNumber && accountNumber.Length >= 8)
        {
            // Mask account number: 1234****5678
            var masked = accountNumber.Substring(0, 4) + 
                        new string('*', accountNumber.Length - 8) + 
                        accountNumber.Substring(accountNumber.Length - 4);
            return masked;
        }
        return "****";
    }
}

// Enhanced currency converter with exchange rates
public class AdvancedCurrencyConverter : ICurrencyConverter
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ILogger<AdvancedCurrencyConverter> _logger;
    
    public AdvancedCurrencyConverter(
        IExchangeRateService exchangeRateService,
        ILogger<AdvancedCurrencyConverter> logger)
    {
        _exchangeRateService = exchangeRateService;
        _logger = logger;
    }
    
    public async Task<string> ConvertAsync(object value, string targetCurrency, CultureInfo culture)
    {
        if (!decimal.TryParse(value?.ToString(), out var amount))
            return "Invalid Amount";
        
        try
        {
            // Convert to target currency if needed
            var convertedAmount = await ConvertCurrencyAsync(amount, "USD", targetCurrency);
            
            // Format according to culture
            var cultureInfo = GetCurrencyCulture(targetCurrency, culture);
            return convertedAmount.ToString("C", cultureInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert currency: {Amount} to {Currency}", amount, targetCurrency);
            return $"{amount:C}"; // Fallback to original formatting
        }
    }
    
    private async Task<decimal> ConvertCurrencyAsync(decimal amount, string fromCurrency, string toCurrency)
    {
        if (fromCurrency.Equals(toCurrency, StringComparison.OrdinalIgnoreCase))
            return amount;
        
        var exchangeRate = await _exchangeRateService.GetExchangeRateAsync(fromCurrency, toCurrency);
        return amount * exchangeRate;
    }
    
    private CultureInfo GetCurrencyCulture(string currencyCode, CultureInfo defaultCulture)
    {
        var currencyCultures = new Dictionary<string, string>
        {
            ["USD"] = "en-US",
            ["EUR"] = "de-DE",
            ["GBP"] = "en-GB",
            ["JPY"] = "ja-JP",
            ["CAD"] = "en-CA",
            ["AUD"] = "en-AU"
        };
        
        if (currencyCultures.TryGetValue(currencyCode.ToUpperInvariant(), out var cultureName))
        {
            try
            {
                return new CultureInfo(cultureName);
            }
            catch
            {
                return defaultCulture;
            }
        }
        
        return defaultCulture;
    }
}

// Date/Time converter with business-specific formats
public class BusinessDateTimeConverter : ICustomConverter
{
    public string Name => "datetime";
    
    public bool CanConvert(string format, Type valueType)
    {
        return (valueType == typeof(DateTime) || valueType == typeof(DateTime?)) &&
               format.StartsWith("datetime:");
    }
    
    public async Task<string> ConvertAsync(object value, string format, CultureInfo culture)
    {
        if (value is not DateTime dateTime)
            return "";
        
        var formatParts = format.Split(':');
        if (formatParts.Length < 2) return dateTime.ToString(culture);
        
        var dateFormat = formatParts[1];
        
        return dateFormat switch
        {
            "business" => FormatBusinessDate(dateTime),
            "relative" => FormatRelativeDate(dateTime),
            "quarter" => FormatQuarter(dateTime),
            "fiscal" => FormatFiscalYear(dateTime),
            "week" => FormatWeekNumber(dateTime),
            _ => dateTime.ToString(dateFormat, culture)
        };
    }
    
    private string FormatBusinessDate(DateTime dateTime)
    {
        return dateTime.ToString("dddd, MMMM dd, yyyy"); // Monday, January 15, 2024
    }
    
    private string FormatRelativeDate(DateTime dateTime)
    {
        var now = DateTime.Now;
        var timeSpan = now - dateTime;
        
        return timeSpan.TotalDays switch
        {
            < 1 when timeSpan.TotalHours < 1 => $"{(int)timeSpan.TotalMinutes} minutes ago",
            < 1 => $"{(int)timeSpan.TotalHours} hours ago",
            < 7 => $"{(int)timeSpan.TotalDays} days ago",
            < 30 => $"{(int)(timeSpan.TotalDays / 7)} weeks ago",
            < 365 => $"{(int)(timeSpan.TotalDays / 30)} months ago",
            _ => $"{(int)(timeSpan.TotalDays / 365)} years ago"
        };
    }
    
    private string FormatQuarter(DateTime dateTime)
    {
        var quarter = (dateTime.Month - 1) / 3 + 1;
        return $"Q{quarter} {dateTime.Year}";
    }
    
    private string FormatFiscalYear(DateTime dateTime)
    {
        // Assuming fiscal year starts in April
        var fiscalYear = dateTime.Month >= 4 ? dateTime.Year : dateTime.Year - 1;
        return $"FY{fiscalYear}";
    }
    
    private string FormatWeekNumber(DateTime dateTime)
    {
        var calendar = CultureInfo.CurrentCulture.Calendar;
        var weekNumber = calendar.GetWeekOfYear(dateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday);
        return $"Week {weekNumber}, {dateTime.Year}";
    }
}
```

#### 5. Performance Optimization and Caching

```csharp
public class OptimizedTransformationService
{
    private readonly ITransformerService _transformerService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OptimizedTransformationService> _logger;
    
    public OptimizedTransformationService(
        ITransformerService transformerService,
        IMemoryCache cache,
        ILogger<OptimizedTransformationService> logger)
    {
        _transformerService = transformerService;
        _cache = cache;
        _logger = logger;
    }
    
    // Cached transformation for frequently used templates
    public async Task<string> TransformWithCacheAsync(
        string template, 
        Dictionary<string, object> data, 
        TimeSpan? cacheExpiration = null)
    {
        var cacheKey = GenerateCacheKey(template, data);
        
        if (_cache.TryGetValue(cacheKey, out string cachedResult))
        {
            _logger.LogDebug("Cache hit for transformation: {CacheKey}", cacheKey);
            return cachedResult;
        }
        
        var result = await _transformerService.TransformAsync(template, data);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = cacheExpiration ?? TimeSpan.FromMinutes(30),
            Priority = CacheItemPriority.Normal,
            Size = EstimateSize(result)
        };
        
        _cache.Set(cacheKey, result, cacheOptions);
        _logger.LogDebug("Cached transformation result: {CacheKey}", cacheKey);
        
        return result;
    }
    
    // Precompile frequently used templates
    public async Task PrecompileTemplatesAsync(IEnumerable<string> templates)
    {
        var compilationTasks = templates.Select(async template =>
        {
            try
            {
                // Extract tokens and validate template
                var tokens = await ExtractAndValidateTokensAsync(template);
                
                // Cache template metadata
                var templateKey = $"template_meta_{template.GetHashCode()}";
                _cache.Set(templateKey, tokens, TimeSpan.FromHours(1));
                
                _logger.LogDebug("Precompiled template with {TokenCount} tokens", tokens.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to precompile template");
            }
        });
        
        await Task.WhenAll(compilationTasks);
    }
    
    // Optimized batch processing with parallel execution
    public async Task<List<string>> BatchTransformOptimizedAsync(
        IEnumerable<(string Template, Dictionary<string, object> Data)> requests)
    {
        var partitioner = Partitioner.Create(requests, true);
        var results = new ConcurrentBag<(int Index, string Result)>();
        
        await Task.Run(() =>
        {
            Parallel.ForEach(partitioner, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, async (request, loop, index) =>
            {
                try
                {
                    var result = await _transformerService.TransformAsync(request.Template, request.Data);
                    results.Add(((int)index, result));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to transform batch item at index: {Index}", index);
                    results.Add(((int)index, $"[Error: {ex.Message}]"));
                }
            });
        });
        
        return results.OrderBy(r => r.Index).Select(r => r.Result).ToList();
    }
    
    private string GenerateCacheKey(string template, Dictionary<string, object> data)
    {
        var dataHash = ComputeDataHash(data);
        var templateHash = template.GetHashCode();
        return $"transform_{templateHash}_{dataHash}";
    }
    
    private string ComputeDataHash(Dictionary<string, object> data)
    {
        var dataString = string.Join("|", data.OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataString));
        return Convert.ToBase64String(hash)[..16]; // Use first 16 characters
    }
    
    private int EstimateSize(string content)
    {
        return Encoding.UTF8.GetByteCount(content);
    }
    
    private async Task<List<Token>> ExtractAndValidateTokensAsync(string template)
    {
        // Implementation would extract and validate tokens
        // This is a placeholder for actual token extraction logic
        return new List<Token>();
    }
}
```

### Advanced Usage Examples

#### 1. Integration with Reporting Systems

```csharp
public class ReportTransformationService
{
    private readonly ITransformerService _transformerService;
    
    public async Task<string> GenerateReportAsync(ReportTemplate template, ReportData data)
    {
        var transformData = new Dictionary<string, object>
        {
            ["report.title"] = data.Title,
            ["report.generatedAt"] = DateTime.UtcNow,
            ["report.generatedBy"] = data.GeneratedBy,
            ["data.items"] = data.Items,
            ["data.summary"] = data.Summary,
            ["formatting.currency"] = data.CurrencyCode,
            ["formatting.culture"] = data.CultureCode
        };
        
        return await _transformerService.TransformAsync(template.Content, transformData);
    }
}
```

#### 2. Email Template Processing

```csharp
public class EmailTemplateProcessor
{
    private readonly ITransformerService _transformerService;
    
    public async Task<ProcessedEmail> ProcessEmailTemplateAsync(
        EmailTemplate template, 
        Customer customer, 
        Order order)
    {
        var context = new Dictionary<string, object>
        {
            ["customer"] = customer,
            ["order"] = order,
            ["company"] = GetCompanyInfo(),
            ["helpers"] = GetTemplateHelpers()
        };
        
        var subject = await _transformerService.TransformAsync(template.Subject, context);
        var body = await _transformerService.TransformAsync(template.Body, context);
        
        return new ProcessedEmail
        {
            To = customer.Email,
            Subject = subject,
            Body = body,
            IsHtml = template.IsHtml
        };
    }
}
```

## Best Practices

### 1. Template Design

```csharp
// Good: Clear, readable template syntax
var template = @"
Dear {customer.name},
Your order #{order.id} has been processed.
Total: {order.total:currency:USD}
Delivery Date: {order.deliveryDate:datetime:business}
";

// Good: Use descriptive token names
var data = new Dictionary<string, object>
{
    ["customer.fullName"] = customer.FullName,
    ["order.totalAmount"] = order.Total,
    ["order.estimatedDelivery"] = order.EstimatedDelivery
};
```

### 2. Performance Optimization

```csharp
// Good: Cache frequently used transformations
public async Task<string> GetCachedTransformationAsync(string template, Dictionary<string, object> data)
{
    var cacheKey = $"transform_{template.GetHashCode()}_{GetDataHash(data)}";
    
    return await _cache.GetOrCreateAsync(cacheKey, async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
        return await _transformerService.TransformAsync(template, data);
    });
}
```

### 3. Error Handling

```csharp
// Good: Graceful error handling with fallbacks
public async Task<string> SafeTransformAsync(string template, Dictionary<string, object> data)
{
    try
    {
        return await _transformerService.TransformAsync(template, data);
    }
    catch (TokenNotFoundException ex)
    {
        _logger.LogWarning("Missing token in template: {Token}", ex.TokenName);
        return template; // Return original template as fallback
    }
    catch (TransformationException ex)
    {
        _logger.LogError(ex, "Transformation failed for template");
        return "[Template processing error]";
    }
}
```

## Integration with Other DKNet Components

DKNet.Svc.Transformation integrates seamlessly with other DKNet components:

- **DKNet.EfCore.Events**: Process domain events through transformation pipelines
- **DKNet.EfCore.Repos**: Transform data retrieved from repositories for display
- **DKNet.SlimBus.Extensions**: Integrate with CQRS for query result transformations
- **DKNet.Svc.BlobStorage.***: Transform and process file content and metadata
- **DKNet.Fw.Extensions**: Leverages core framework utilities for type handling and extensions

---

> 💡 **Transformation Tip**: Use DKNet.Svc.Transformation to create powerful, flexible data processing pipelines while maintaining clean separation between business logic and presentation concerns. Always validate templates before deployment, implement proper error handling with fallbacks, and consider caching for frequently used transformations. The extensible converter architecture allows you to implement domain-specific formatting rules that align with your business requirements.