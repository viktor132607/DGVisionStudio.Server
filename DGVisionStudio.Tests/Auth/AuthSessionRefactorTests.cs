using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Auth;

public sealed class AuthSessionRefactorTests
{
    [Fact]
    public async Task LoginService_PreservesRequiredFieldAndUnknownUserResponses()
    {
        var manager = new ConfigurableUserManager();
        var signIn = new ConfigurableSignInManager(manager);
        var service = new AuthLoginService(
            manager,
            signIn,
            NullLogger<AuthSessionService>.Instance);

        var missing = await service.LoginAsync(
            new LoginRequest(),
            "trace-missing");

        var unknown = await service.LoginAsync(
            new LoginRequest
            {
                Email = "missing@example.com",
                Password = "Password1!"
            },
            "trace-unknown");

        missing.StatusCode.Should()
            .Be(StatusCodes.Status400BadRequest);
        unknown.StatusCode.Should()
            .Be(StatusCodes.Status401Unauthorized);
        signIn.LastUserName.Should().BeNull();
    }

    [Fact]
    public async Task LoginService_PreservesBlockedAndLockedAccountPolicy()
    {
        var blocked = TestUsers.Create(
            "blocked@example.com",
            "blocked-1");
        blocked.IsBlocked = true;

        var blockedManager =
            new ConfigurableUserManager([blocked]);
        var blockedSignIn =
            new ConfigurableSignInManager(blockedManager);
        var blockedService = new AuthLoginService(
            blockedManager,
            blockedSignIn,
            NullLogger<AuthSessionService>.Instance);

        var blockedResult = await blockedService.LoginAsync(
            new LoginRequest
            {
                Email = blocked.Email!,
                Password = "Password1!"
            },
            "trace-blocked");

        blockedResult.StatusCode.Should()
            .Be(StatusCodes.Status401Unauthorized);
        blockedSignIn.LastUserName.Should().BeNull();

        var locked = TestUsers.Create(
            "locked@example.com",
            "locked-1");
        var lockedManager =
            new ConfigurableUserManager([locked])
            {
                IsLockedOutResult = true
            };
        var lockedSignIn =
            new ConfigurableSignInManager(lockedManager);
        var lockedService = new AuthLoginService(
            lockedManager,
            lockedSignIn,
            NullLogger<AuthSessionService>.Instance);

        var lockedResult = await lockedService.LoginAsync(
            new LoginRequest
            {
                Email = locked.Email!,
                Password = "Password1!"
            },
            "trace-locked");

        lockedResult.StatusCode.Should()
            .Be(StatusCodes.Status423Locked);
        lockedSignIn.LastUserName.Should().BeNull();
    }

    [Fact]
    public async Task LoginService_PreservesSignInLockedFailureAndSuccessfulRoles()
    {
        var user = TestUsers.Create(
            "person@example.com",
            "user-1");
        var manager = new ConfigurableUserManager([user]);
        manager.SetRoles(user, "Admin", "User");

        var signIn = new ConfigurableSignInManager(manager)
        {
            PasswordSignInResult = SignInResult.LockedOut
        };
        var service = new AuthLoginService(
            manager,
            signIn,
            NullLogger<AuthSessionService>.Instance);

        var locked = await service.LoginAsync(
            Request(user),
            "trace-locked-after-signin");

        locked.StatusCode.Should()
            .Be(StatusCodes.Status423Locked);

        signIn.PasswordSignInResult = SignInResult.Failed;

        var failed = await service.LoginAsync(
            Request(user),
            "trace-failed");

        failed.StatusCode.Should()
            .Be(StatusCodes.Status401Unauthorized);

        signIn.PasswordSignInResult = SignInResult.Success;

        var success = await service.LoginAsync(
            Request(user),
            "trace-success");

        success.StatusCode.Should().Be(StatusCodes.Status200OK);
        success.Value!.GetType()
            .GetProperty("roles")!
            .GetValue(success.Value)
            .Should().BeEquivalentTo(
                new[] { "Admin", "User" });
    }

    [Fact]
    public async Task StateService_PreservesAnonymousCurrentUserAndLogout()
    {
        var user = TestUsers.Create(
            "person@example.com",
            "user-1");
        var manager = new ConfigurableUserManager([user])
        {
            CurrentUser = user
        };
        manager.SetRoles(user, "User");

        var signIn = new ConfigurableSignInManager(manager);
        var service = new AuthSessionStateService(
            manager,
            signIn,
            NullLogger<AuthSessionService>.Instance);

        var anonymous = await service.GetCurrentUserAsync(
            new AuthRequestContext(
                TestUsers.CreatePrincipal(null),
                "trace-anon"));

        anonymous.StatusCode.Should().Be(StatusCodes.Status200OK);
        anonymous.Value!.GetType()
            .GetProperty("isAuthenticated")!
            .GetValue(anonymous.Value)
            .Should().Be(false);

        var authenticated = await service.GetCurrentUserAsync(
            new AuthRequestContext(
                TestUsers.CreatePrincipal(user),
                "trace-user"));

        authenticated.StatusCode.Should()
            .Be(StatusCodes.Status200OK);
        authenticated.Value!.GetType()
            .GetProperty("isAuthenticated")!
            .GetValue(authenticated.Value)
            .Should().Be(true);

        var logout = await service.LogoutAsync(
            new AuthRequestContext(
                TestUsers.CreatePrincipal(user),
                "trace-logout"));

        logout.StatusCode.Should().Be(StatusCodes.Status200OK);
        signIn.SignedOut.Should().BeTrue();
    }

    [Fact]
    public async Task FacadeCompatibilityConstructor_PreservesLoginFlow()
    {
        var user = TestUsers.Create(
            "person@example.com",
            "user-1");
        var manager = new ConfigurableUserManager([user]);
        var signIn = new ConfigurableSignInManager(manager)
        {
            PasswordSignInResult = SignInResult.Success
        };

        var service = new AuthSessionService(
            manager,
            signIn,
            NullLogger<AuthSessionService>.Instance);

        var result = await service.LoginAsync(
            Request(user),
            "trace");

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        signIn.LastUserName.Should().Be(user.UserName);
    }

    private static LoginRequest Request(
        ApplicationUser user) =>
        new()
        {
            Email = user.Email!,
            Password = "Password1!"
        };
}
