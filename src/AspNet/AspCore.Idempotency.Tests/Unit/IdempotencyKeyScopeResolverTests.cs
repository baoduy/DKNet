// <copyright file="IdempotencyKeyScopeResolverTests.cs" company="https://drunkcoding.net">
// Copyright (c) 2025 Steven Hoang. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

using System.Net;
using System.Security.Claims;
using DKNet.AspCore.Idempotency;
using Microsoft.AspNetCore.Http;

namespace AspCore.Idempotency.Tests.Unit;

public class IdempotencyKeyScopeResolverTests
{
    #region Methods

    [Fact]
    public void Resolve_AuthenticatedUserWithNameIdentifier_ReturnsUserScope()
    {
        // Arrange
        var context = new DefaultHttpContext { User = CreateUser("user-42", authenticated: true) };
        var options = new IdempotencyOptions();

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe("user:user-42");
    }

    [Fact]
    public void Resolve_AuthenticatedUser_TakesPrecedenceOverAuthHeader()
    {
        // Arrange
        var context = new DefaultHttpContext { User = CreateUser("user-42", authenticated: true) };
        context.Request.Headers.Authorization = "Bearer tok-1";
        var options = new IdempotencyOptions { ScopeHmacSecret = "s3cret" };

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe("user:user-42");
    }

    [Fact]
    public void Resolve_AuthenticatedUserWithoutNameIdentifier_FallsThroughToAuthHeader()
    {
        // Arrange
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test"))
        };
        context.Request.Headers.Authorization = "Bearer tok-1";
        var options = new IdempotencyOptions { ScopeHmacSecret = "s3cret" };

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe("auth:0bf9c2aa46f13c91e6501d77fdd0da0c3026ae620f9161ef812857ffd8a71a30");
    }

    [Fact]
    public void Resolve_AuthenticatedUserWithWhitespaceNameIdentifier_FallsThrough()
    {
        // Arrange
        var context = new DefaultHttpContext { User = CreateUser("   ", authenticated: true) };
        var options = new IdempotencyOptions();

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe(string.Empty);
    }

    [Fact]
    public void Resolve_UnauthenticatedUserWithNameIdentifier_FallsThrough()
    {
        // Arrange
        var context = new DefaultHttpContext { User = CreateUser("user-42", authenticated: false) };
        context.Request.Headers.Authorization = "Bearer tok-1";
        var options = new IdempotencyOptions { ScopeHmacSecret = "s3cret" };

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe("auth:0bf9c2aa46f13c91e6501d77fdd0da0c3026ae620f9161ef812857ffd8a71a30");
    }

    [Fact]
    public void Resolve_UnauthenticatedUserWithAuthHeaderAndSecret_ReturnsHmacScope()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer tok-1";
        var options = new IdempotencyOptions { ScopeHmacSecret = "s3cret" };

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe("auth:0bf9c2aa46f13c91e6501d77fdd0da0c3026ae620f9161ef812857ffd8a71a30");
    }

    [Fact]
    public void Resolve_UnauthenticatedUserWithEmptyAuthHeader_FallsThrough()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "";
        var options = new IdempotencyOptions { ScopeHmacSecret = "s3cret" };

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe(string.Empty);
    }

    [Fact]
    public void Resolve_UnauthenticatedUserWithWhitespaceAuthHeader_FallsThrough()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "   ";
        var options = new IdempotencyOptions { ScopeHmacSecret = "s3cret" };

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe(string.Empty);
    }

    [Fact]
    public void Resolve_AuthHeaderPresentButNoSecret_DoesNotProduceBareHash()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer tok-1";
        var options = new IdempotencyOptions();

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe(string.Empty);
    }

    [Fact]
    public void Resolve_IncludeClientIpInScopeTrue_ReturnsIpScope()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        var options = new IdempotencyOptions { IncludeClientIpInScope = true };

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe("ip:192.0.2.10");
    }

    [Fact]
    public void Resolve_IncludeClientIpInScopeFalse_ReturnsEmpty()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        var options = new IdempotencyOptions();

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe(string.Empty);
    }

    [Fact]
    public void Resolve_IncludeClientIpInScopeTrueButRemoteIpNull_ReturnsEmpty()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var options = new IdempotencyOptions { IncludeClientIpInScope = true };

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe(string.Empty);
    }

    [Fact]
    public void Resolve_NoUserNoHeaderNoIp_ReturnsEmpty()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var options = new IdempotencyOptions();

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe(string.Empty);
    }

    [Fact]
    public void Resolve_NullUser_ReturnsEmpty()
    {
        // Arrange
        var context = new DefaultHttpContext { User = null! };
        var options = new IdempotencyOptions();

        // Act
        var scope = IdempotencyKeyScopeResolver.Resolve(context, options);

        // Assert
        scope.ShouldBe(string.Empty);
    }

    #endregion

    #region Helpers

    private static ClaimsPrincipal CreateUser(string nameIdentifier, bool authenticated) =>
        new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, nameIdentifier) },
            authenticated ? "Test" : null));

    #endregion
}
