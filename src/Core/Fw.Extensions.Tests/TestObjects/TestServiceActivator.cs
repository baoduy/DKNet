using Microsoft.Extensions.Logging;

namespace Fw.Extensions.Tests.TestObjects;

public interface ITestInterface;

public class TestClass : ITestInterface;

public class TestServiceActivator(
    IServiceProvider provider,
    ILogger<TestServiceActivator> logger,
    IEnumerable<ITestInterface> interfaces)
{
    #region Properties

    public ILogger<TestServiceActivator> Logger1 { get; } = logger ?? throw new ArgumentNullException(nameof(logger));

    public IServiceProvider Provider { get; } = provider ?? throw new ArgumentNullException(nameof(provider));

    public ITestInterface Interface { get; } =
        interfaces.FirstOrDefault() ?? throw new ArgumentNullException(nameof(interfaces));

    #endregion
}