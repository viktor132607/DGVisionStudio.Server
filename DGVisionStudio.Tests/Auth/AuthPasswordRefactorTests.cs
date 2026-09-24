using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Auth;

public sealed class AuthPasswordRefactorTests
{
    [Fact]
    public void ResetLinkService_BuildsEncodedApiAndFrontendUrls()
    {
        var service = new AuthPasswordResetLinkService(
            TestConfiguration.Create(
                ("Api:Url", "https://api.example/"),
                ("Frontend:Url", "https://studio.example/")));

        service.BuildApiResetUrl(
                "person+tag@example.com",
                "token /+")
            .Should()
            .Be("https://api.example/api/auth/reset-password?email=person%2Btag%40example.com&token=token%20%2F%2B");

        service.GetFrontendRedirectUrl(
                "person+tag@example.com",
                "token /+")
            .Should()
            .Be("https://studio.example/identity/reset-password?email=person%2Btag%40example.com&token=token%20%2F%2B");

        service.GetFrontendRedirectUrl("", "")
            .Should()
            .Be("https://studio.example/identity/login");
    }

    [Fact]
    public async Task ForgotPasswordService_HidesMissingAndBlockedAccounts()
    {
        var blocked = TestUsers.Create(
            "blocked@example.com",
            "blocked");
        blocked.IsBlocked = true;

        var manager = new ConfigurableUserManager([blocked]);
        var email = new RecordingEmailService();
        var links = new AuthPasswordResetLinkService(
            TestConfiguration.Create(
                ("Api:Url", "https://api.example")));

        var service = new AuthForgotPasswordService(
            manager,
            email,
            links,
            NullLogger<AuthForgotPasswordService>.Instance);

        var missing = await service.ForgotPasswordAsync(
            new ForgotPasswordRequest
            {
                Email = "missing@example.com"
            },
            "trace");

        var blockedResult = await service.ForgotPasswordAsync(
            new ForgotPasswordRequest
            {
                Email = blocked.Email!
            },
            "trace");

        missing.StatusCode.Should().Be(StatusCodes.Status200OK);
        blockedResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        email.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ResetPasswordService_CoversMismatchIdentityFailureAndSuccess()
    {
        var user = TestUsers.Create(
            "person@example.com",
            "user-1");
        var manager = new ConfigurableUserManager([user]);
        var service = new AuthResetPasswordService(
            manager,
            NullLogger<AuthResetPasswordService>.Instance);

        var mismatch = await service.ResetPasswordAsync(
            new ResetPasswordRequest
            {
                Email = user.Email!,
                Token = "token",
                Password = "One1!",
                ConfirmPassword = "Two2!"
            },
            "trace");

        manager.ResetPasswordResult = IdentityResult.Failed(
            new IdentityError
            {
                Description = "Invalid token"
            });

        var failed = await service.ResetPasswordAsync(
            ValidReset(user.Email!),
            "trace");

        manager.ResetPasswordResult = IdentityResult.Success;
        var success = await service.ResetPasswordAsync(
            ValidReset(user.Email!),
            "trace");

        mismatch.StatusCode.Should().Be(
            StatusCodes.Status400BadRequest);
        failed.StatusCode.Should().Be(
            StatusCodes.Status400BadRequest);
        success.StatusCode.Should().Be(
            StatusCodes.Status200OK);
    }

    [Fact]
    public async Task ChangePasswordService_RejectsBlockedAndHandlesIdentityResult()
    {
        var user = TestUsers.Create(
            "person@example.com",
            "user-1");
        var manager = new ConfigurableUserManager([user])
        {
            CurrentUser = user
        };
        var service = new AuthChangePasswordService(
            manager,
            NullLogger<AuthChangePasswordService>.Instance);
        var context = new AuthRequestContext(
            TestUsers.CreatePrincipal(user),
            "trace");

        user.IsBlocked = true;
        var blocked = await service.ChangePasswordAsync(
            ValidChange(),
            context);

        user.IsBlocked = false;
        manager.ChangePasswordResult = IdentityResult.Failed(
            new IdentityError
            {
                Description = "Wrong current password"
            });

        var failed = await service.ChangePasswordAsync(
            ValidChange(),
            context);

        manager.ChangePasswordResult = IdentityResult.Success;
        var success = await service.ChangePasswordAsync(
            ValidChange(),
            context);

        blocked.StatusCode.Should().Be(
            StatusCodes.Status401Unauthorized);
        failed.StatusCode.Should().Be(
            StatusCodes.Status400BadRequest);
        success.StatusCode.Should().Be(
            StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Facade_DelegatesPasswordOperationsToFocusedServices()
    {
        var user = TestUsers.Create(
            "person@example.com",
            "user-1");
        var manager = new ConfigurableUserManager([user])
        {
            CurrentUser = user
        };
        var email = new RecordingEmailService();
        var links = new AuthPasswordResetLinkService(
            TestConfiguration.Create(
                ("Api:Url", "https://api.example"),
                ("Frontend:Url", "https://studio.example")));

        var facade = new AuthPasswordService(
            new AuthForgotPasswordService(
                manager,
                email,
                links,
                NullLogger<AuthForgotPasswordService>.Instance),
            links,
            new AuthResetPasswordService(
                manager,
                NullLogger<AuthResetPasswordService>.Instance),
            new AuthChangePasswordService(
                manager,
                NullLogger<AuthChangePasswordService>.Instance));

        var forgot = await facade.ForgotPasswordAsync(
            new ForgotPasswordRequest
            {
                Email = user.Email!
            },
            "trace");

        var reset = await facade.ResetPasswordAsync(
            ValidReset(user.Email!),
            "trace");

        var changed = await facade.ChangePasswordAsync(
            ValidChange(),
            new AuthRequestContext(
                TestUsers.CreatePrincipal(user),
                "trace"));

        forgot.StatusCode.Should().Be(StatusCodes.Status200OK);
        reset.StatusCode.Should().Be(StatusCodes.Status200OK);
        changed.StatusCode.Should().Be(StatusCodes.Status200OK);
        email.Messages.Should().ContainSingle();

        facade.GetResetPasswordRedirectUrl(
                user.Email!,
                "token")
            .Should()
            .StartWith(
                "https://studio.example/identity/reset-password");
    }

    private static ResetPasswordRequest ValidReset(string email) =>
        new()
        {
            Email = email,
            Token = "token",
            Password = "Password1!",
            ConfirmPassword = "Password1!"
        };

    private static ChangePasswordRequest ValidChange() =>
        new()
        {
            CurrentPassword = "OldPassword1!",
            NewPassword = "NewPassword1!",
            ConfirmPassword = "NewPassword1!"
        };
}
