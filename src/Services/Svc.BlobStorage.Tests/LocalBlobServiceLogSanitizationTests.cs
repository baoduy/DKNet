// <copyright file="LocalBlobServiceLogSanitizationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using DKNet.Svc.BlobStorage.Local;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Svc.BlobStorage.Tests;

/// <summary>
///     Covers the log-forging fix (CWE-117): a caller-supplied blob name carrying a control
///     character must never inject a raw line break into the missing-folder log record.
/// </summary>
public class LocalBlobServiceLogSanitizationTests : IDisposable
{
    #region Fields

    private readonly string _root;

    #endregion

    #region Constructors

    public LocalBlobServiceLogSanitizationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "DKNet-LocalBlobLogSanitization-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    #endregion

    #region Methods

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task DeleteAsync_MissingFolderNameContainsNewline_LogsSingleLineSanitizedRecord()
    {
        // Arrange: "\n" is a legal path segment on Linux/macOS, so it survives GetFinalPath
        // unchanged and reaches the missing-folder log call unless sanitized there.
        var capturingLogger = new CapturingLogger();
        var options = Options.Create(new LocalDirectoryOptions { RootFolder = _root });
        var service = new LocalBlobService(options, capturingLogger);
        const string forgedSegment = "missing\n2026-09-18 00:00:00 ERROR Administrator deleted all blobs";

        // Act
        var result = await service.DeleteAsync(new BlobRequest(forgedSegment) { Type = BlobTypes.Directory });

        // Assert: still returns false (behaviour unchanged), exactly one Error record, and neither
        // the formatted message nor the structured FolderLocation value carries a raw line break.
        result.ShouldBeFalse();
        capturingLogger.Records.Count.ShouldBe(1);
        var record = capturingLogger.Records[0];
        record.Level.ShouldBe(LogLevel.Error);
        record.Message.ShouldNotContain('\n');
        record.Message.ShouldNotContain('\r');
        ((string)record.FolderLocation!).ShouldNotContain('\n');
        ((string)record.FolderLocation!).ShouldNotContain('\r');
    }

    #endregion

    #region Nested Types

    /// <summary>
    ///     Minimal capturing <see cref="ILogger{TCategoryName}" /> that records the log level, formatted
    ///     message, and structured "FolderLocation" value of every emitted record, for assertion.
    /// </summary>
    private sealed class CapturingLogger : ILogger<LocalBlobService>
    {
        public List<(LogLevel Level, string Message, object? FolderLocation)> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var folderLocation = state is IEnumerable<KeyValuePair<string, object>> pairs
                ? pairs.FirstOrDefault(p => p.Key == "FolderLocation").Value
                : null;
            Records.Add((logLevel, formatter(state, exception), folderLocation));
        }
    }

    #endregion
}
