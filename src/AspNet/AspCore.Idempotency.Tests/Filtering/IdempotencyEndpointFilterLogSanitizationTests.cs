// <copyright file="IdempotencyEndpointFilterLogSanitizationTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Security.Claims;
using DKNet.AspCore.Idempotency;
using DKNet.AspCore.Idempotency.Filtering;
using DKNet.AspCore.Idempotency.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

namespace AspCore.Idempotency.Tests.Filtering;

/// <summary>
///     Covers the log-forging and 409-scope-leak fix: an idempotency key that passes format
///     validation (including the trailing-newline edge case allowed by the default regex anchor)
///     must never inject a raw line break into a log entry, and a duplicate-request 409 body must
///     never surface the caller scope.
/// </summary>
public class IdempotencyEndpointFilterLogSanitizationTests
{
    #region Methods

    [Fact]
    public async Task InvokeAsync_WhenKeyEndsWithSingleTrailingNewline_PassesValidationAndLogsOnlySingleLineMessages()
    {
        // Arrange - "$" without RegexOptions.Multiline still matches before a single trailing '\n',
        // so this key passes IsValid and reaches the code paths that log it.
        const string key = "order-42\n";
        var store = new FakeIdempotencyKeyStore();
        var logger = new CapturingLogger();
        var filter = new IdempotencyEndpointFilter(store, Options.Create(new IdempotencyOptions()), logger);
        var context = CreateContext(key);
        var nextInvoked = false;
        EndpointFilterDelegate next = _ =>
        {
            nextInvoked = true;
            return ValueTask.FromResult<object?>(TypedResults.Ok(new { ok = true }));
        };

        // Act
        await filter.InvokeAsync(context, next);

        // Assert
        nextInvoked.ShouldBeTrue();
        logger.Messages.ShouldNotBeEmpty();
        logger.Messages.ShouldAllBe(m => !m.Message.Contains('\n') && !m.Message.Contains('\r'));
    }

    [Fact]
    public async Task
        InvokeAsync_WhenAuthenticatedCallersDuplicateKeyReturnsConflict_ProblemDetailExcludesScopeAndUnsafeKey()
    {
        // Arrange
        const string key = "order-42";
        var options = new IdempotencyOptions { ConflictHandling = IdempotentConflictHandling.ConflictResponse };
        var store = new FakeIdempotencyKeyStore { ProcessedResult = (true, null) };
        var logger = new CapturingLogger();
        var filter = new IdempotencyEndpointFilter(store, Options.Create(options), logger);
        var context = CreateContext(key, AuthenticatedUser("user-42"));
        EndpointFilterDelegate next = _ =>
            throw new InvalidOperationException("A duplicate request must not reach the endpoint handler.");

        // Act
        var result = await filter.InvokeAsync(context, next);

        // Assert
        var problem = result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        problem.ProblemDetails.Detail.ShouldNotBeNull();
        problem.ProblemDetails.Detail.ShouldContain(key);
        problem.ProblemDetails.Detail.ShouldNotContain("user:");
        problem.ProblemDetails.Detail.ShouldNotContain("user-42");
    }

    [Fact]
    public async Task
        InvokeAsync_WhenResponseSerializationThrows_LogsSingleLineWarningAndReturnsResultUncached()
    {
        // Arrange - a self-referencing object graph fails System.Text.Json's default cycle
        // detection, driving the JsonException catch branch of CacheResponseAsync.
        const string key = "order-42\n";
        var store = new FakeIdempotencyKeyStore();
        var logger = new CapturingLogger();
        var filter = new IdempotencyEndpointFilter(store, Options.Create(new IdempotencyOptions()), logger);
        var context = CreateContext(key);
        var cyclic = new CyclicNode();
        cyclic.Self = cyclic;
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(TypedResults.Ok(cyclic));

        // Act
        var result = await filter.InvokeAsync(context, next);

        // Assert
        result.ShouldNotBeNull();
        logger.Messages.ShouldNotBeEmpty();
        logger.Messages.ShouldAllBe(m => !m.Message.Contains('\n') && !m.Message.Contains('\r'));
    }

    [Fact]
    public async Task
        InvokeAsync_WhenMarkKeyAsProcessedThrows_LogsSingleLineErrorAndReturnsResult()
    {
        // Arrange - drives the catch (Exception ex) branch of CacheResponseAsync, covering the
        // "unexpected error while caching" log site.
        const string key = "order-42\n";
        var store = new FakeIdempotencyKeyStore { ThrowOnMarkProcessed = true };
        var logger = new CapturingLogger();
        var filter = new IdempotencyEndpointFilter(store, Options.Create(new IdempotencyOptions()), logger);
        var context = CreateContext(key);
        EndpointFilterDelegate next = _ => ValueTask.FromResult<object?>(TypedResults.Ok(new { ok = true }));

        // Act
        var result = await filter.InvokeAsync(context, next);

        // Assert
        result.ShouldNotBeNull();
        logger.Messages.ShouldNotBeEmpty();
        logger.Messages.ShouldAllBe(m => !m.Message.Contains('\n') && !m.Message.Contains('\r'));
    }

    private static ClaimsPrincipal AuthenticatedUser(string userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)],
            "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static EndpointFilterInvocationContext CreateContext(string idempotencyKey, ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext
        {
            Request = { Method = "POST", Path = "/api/orders" }
        };
        httpContext.Request.Headers["X-Idempotency-Key"] = idempotencyKey;
        if (user is not null) httpContext.User = user;

        return EndpointFilterInvocationContext.Create(httpContext);
    }

    #endregion

    #region Nested Types

    private sealed class FakeIdempotencyKeyStore : IIdempotencyKeyStore
    {
        public (bool processed, CachedResponse? response) ProcessedResult { get; set; } = (false, null);

        public bool ThrowOnMarkProcessed { get; set; }

        public ValueTask<(bool processed, CachedResponse? response)> IsKeyProcessedAsync(IdempotentKeyInfo keyInfo) =>
            ValueTask.FromResult(ProcessedResult);

        public ValueTask MarkKeyAsProcessedAsync(IdempotentKeyInfo keyInfo, CachedResponse cachedResponse)
        {
            if (ThrowOnMarkProcessed) throw new InvalidOperationException("store unavailable");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CyclicNode
    {
        public CyclicNode? Self { get; set; }
    }

    private sealed class CapturingLogger : ILogger<IdempotencyEndpointFilter>
    {
        private readonly List<(LogLevel Level, string Message)> _messages = [];

        public IReadOnlyList<(LogLevel Level, string Message)> Messages => _messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _messages.Add((logLevel, formatter(state, exception)));
    }

    #endregion
}
