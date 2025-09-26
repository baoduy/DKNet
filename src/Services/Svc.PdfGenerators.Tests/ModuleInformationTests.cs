using DKNet.Svc.PdfGenerators.Models;

namespace Svc.PdfGenerators.Tests;

public class ModuleInformationTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange
        var remotePath = "https://cdn.jsdelivr.net/npm/module@1.0.0/dist/module.min.js";
        var nodePath = "node_modules/module/dist/module.min.js";

        // Act
        var moduleInfo = new ModuleInformation(remotePath, nodePath);

        // Assert
        Assert.Equal(remotePath, moduleInfo.RemotePath);
        Assert.Equal(nodePath, moduleInfo.NodePath);
    }

    [Fact]
    public void Constructor_WithEmptyStrings_HandlesCorrectly()
    {
        // Arrange
        var remotePath = "";
        var nodePath = "";

        // Act
        var moduleInfo = new ModuleInformation(remotePath, nodePath);

        // Assert
        Assert.Equal(remotePath, moduleInfo.RemotePath);
        Assert.Equal(nodePath, moduleInfo.NodePath);
    }

    [Fact]
    public void Properties_AreReadOnly()
    {
        // Arrange
        var moduleInfo = new ModuleInformation("remote", "node");

        // Act & Assert
        var remoteProperty = typeof(ModuleInformation).GetProperty(nameof(ModuleInformation.RemotePath));
        var nodeProperty = typeof(ModuleInformation).GetProperty(nameof(ModuleInformation.NodePath));

        Assert.NotNull(remoteProperty);
        Assert.NotNull(nodeProperty);
        Assert.True(remoteProperty.CanRead);
        Assert.False(remoteProperty.CanWrite);
        Assert.True(nodeProperty.CanRead);
        Assert.False(nodeProperty.CanWrite);
    }

    [Fact]
    public void UpdateDic_WithValidDictionary_UpdatesNodePaths()
    {
        // Arrange
        var originalDictionary = new Dictionary<string, ModuleInformation>
        {
            { "module1", new ModuleInformation("https://example.com/module1.js", "module1/index.js") },
            { "module2", new ModuleInformation("https://example.com/module2.js", "module2/index.js") }
        };
        var basePath = "/custom/base/path";

        // Act
        var updatedDictionary = ModuleInformation.UpdateDic(originalDictionary, basePath);

        // Assert
        Assert.Equal(2, updatedDictionary.Count);

        Assert.True(updatedDictionary.ContainsKey("module1"));
        Assert.Equal("https://example.com/module1.js", updatedDictionary["module1"].RemotePath);
        Assert.Equal(Path.Combine(basePath, "module1/index.js"), updatedDictionary["module1"].NodePath);

        Assert.True(updatedDictionary.ContainsKey("module2"));
        Assert.Equal("https://example.com/module2.js", updatedDictionary["module2"].RemotePath);
        Assert.Equal(Path.Combine(basePath, "module2/index.js"), updatedDictionary["module2"].NodePath);
    }

    [Fact]
    public void UpdateDic_WithEmptyDictionary_ReturnsEmptyDictionary()
    {
        // Arrange
        var originalDictionary = new Dictionary<string, ModuleInformation>();
        var basePath = "/some/path";

        // Act
        var updatedDictionary = ModuleInformation.UpdateDic(originalDictionary, basePath);

        // Assert
        Assert.Empty(updatedDictionary);
    }

    [Fact]
    public void UpdateDic_WithEmptyBasePath_UsesRelativePaths()
    {
        // Arrange
        var originalDictionary = new Dictionary<string, ModuleInformation>
        {
            { "test", new ModuleInformation("https://example.com/test.js", "test/index.js") }
        };
        var basePath = "";

        // Act
        var updatedDictionary = ModuleInformation.UpdateDic(originalDictionary, basePath);

        // Assert
        Assert.Single(updatedDictionary);
        Assert.Equal("test/index.js", updatedDictionary["test"].NodePath);
    }

    [Fact]
    public void UpdateDic_WithDifferentKeyTypes_WorksWithIntegerKeys()
    {
        // Arrange
        var originalDictionary = new Dictionary<int, ModuleInformation>
        {
            { 1, new ModuleInformation("https://example.com/module1.js", "module1.js") },
            { 2, new ModuleInformation("https://example.com/module2.js", "module2.js") }
        };
        var basePath = "/test/path";

        // Act
        var updatedDictionary = ModuleInformation.UpdateDic(originalDictionary, basePath);

        // Assert
        Assert.Equal(2, updatedDictionary.Count);
        Assert.True(updatedDictionary.ContainsKey(1));
        Assert.True(updatedDictionary.ContainsKey(2));
        Assert.Equal(Path.Combine(basePath, "module1.js"), updatedDictionary[1].NodePath);
        Assert.Equal(Path.Combine(basePath, "module2.js"), updatedDictionary[2].NodePath);
    }

    [Fact]
    public void UpdateDic_RemotePathsRemainUnchanged()
    {
        // Arrange
        var originalDictionary = new Dictionary<string, ModuleInformation>
        {
            { "module", new ModuleInformation("https://original.com/module.js", "original/path.js") }
        };
        var basePath = "/new/base/path";

        // Act
        var updatedDictionary = ModuleInformation.UpdateDic(originalDictionary, basePath);

        // Assert
        Assert.Equal("https://original.com/module.js", updatedDictionary["module"].RemotePath);
        Assert.NotEqual("original/path.js", updatedDictionary["module"].NodePath);
        Assert.Equal(Path.Combine(basePath, "original/path.js"), updatedDictionary["module"].NodePath);
    }

    [Fact]
    public void UpdateDic_WithComplexPaths_HandlesCorrectly()
    {
        // Arrange
        var originalDictionary = new Dictionary<string, ModuleInformation>
        {
            { "complex", new ModuleInformation("https://example.com/module.js", "sub/folder/file.js") }
        };
        var basePath = "/complex/base/path";

        // Act
        var updatedDictionary = ModuleInformation.UpdateDic(originalDictionary, basePath);

        // Assert
        var expectedPath = Path.Combine(basePath, "sub/folder/file.js");
        Assert.Equal(expectedPath, updatedDictionary["complex"].NodePath);
    }

    [Fact]
    public void UpdateDic_WithNullValues_ThrowsException()
    {
        // Arrange
        Dictionary<string, ModuleInformation>? nullDictionary = null;
        var basePath = "/some/path";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            ModuleInformation.UpdateDic(nullDictionary!, basePath));
    }

    [Theory]
    [InlineData("C:\\Windows\\Path", "module.js")]
    [InlineData("/unix/path", "module.js")]
    [InlineData("relative/path", "module.js")]
    public void UpdateDic_WithDifferentBasePathStyles_HandlesCorrectly(string basePath, string nodePath)
    {
        // Arrange
        var originalDictionary = new Dictionary<string, ModuleInformation>
        {
            { "test", new ModuleInformation("https://example.com/test.js", nodePath) }
        };

        // Act
        var updatedDictionary = ModuleInformation.UpdateDic(originalDictionary, basePath);

        // Assert
        var expectedPath = Path.Combine(basePath, nodePath);
        Assert.Equal(expectedPath, updatedDictionary["test"].NodePath);
    }
}