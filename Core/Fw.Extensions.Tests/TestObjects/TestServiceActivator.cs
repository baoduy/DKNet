using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Fw.Extensions.Tests.TestObjects;

public interface ITestInterface;

public class TestClass : ITestInterface;

public class TestServiceActivator(
    IServiceProvider provider,
    ILogger<TestServiceActivator> logger,
    IEnumerable<ITestInterface> interfaces)
{
    private readonly IServiceProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    private readonly ILogger<TestServiceActivator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly ITestInterface _interface = interfaces.FirstOrDefault()?? throw new ArgumentNullException(nameof(interfaces));
}