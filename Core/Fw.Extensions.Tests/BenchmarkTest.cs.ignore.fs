using BenchmarkDotNet.Running;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Framework.Extensions.Tests;

[TestClass]
public class BenchmarkTest
{


    [TestMethod]
    public void TestTypeExtractor()
    {
        var summary = BenchmarkRunner.Run<TestTypeExtractorExtensions>();
    }


}