using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Auth;

public sealed class AuthRegistrationServiceTests
{
    [Fact]
    public async Task RegisterAsync_RejectsMissingFields()
    {
        var manager = new ConfigurableUserManager();
        var service = new AuthRegistrationService(
            manager,
            TestConfiguration.Create(),
            NullLogger<AuthRegistrationService>.Instance);

        var result = await service.RegisterAsync(new RegisterRequest(), "trace");

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        manager.CreatedUser.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_CreatesTrimmedUserAndAddsUserRole()
    {
        var manager = new ConfigurableUserManager();
        var service = new AuthRegistrationService(
            manager,
            TestConfiguration.Create(("Frontend:Url", "https://studio.example/")),
            NullLogger<AuthRegistrationService>.Instance);

        var result = await service.RegisterAsync(new RegisterRequest
        {
            Email = "  person@example.com  ",
            Password = "Password1!",
            ConfirmPassword = "Password1!"
        }, "trace");

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        manager.CreatedUser.Should().NotBeNull();
        manager.CreatedUser!.Email.Should().Be("person@example.com");
        manager.CreatedUser.EmailConfirmed.Should().BeTrue();
        manager.AddedRoles.Should().ContainSingle(x => x.Role == "User");
        service.GetConfirmEmailRedirectUrl().Should().Be("https://studio.example/identity/login");
    }
}

public sealed class AuthSessionServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsUnauthorized_ForUnknownEmail()
    {
        var manager = new ConfigurableUserManager();
        var signIn = new ConfigurableSignInManager(manager);
        var service = new AuthSessionService(
            manager,
            signIn,
            NullLogger<AuthSessionService>.Instance);

        var result = await service.LoginAsync(new LoginRequest
        {
            Email = "missing@example.com",
            Password = "wrong"
        }, "trace");

        result.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        signIn.LastUserName.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ReturnsRoles_WhenPasswordSignInSucceeds()
    {
        var user = TestUsers.Create("admin@example.com", "admin-1");
        var manager = new ConfigurableUserManager([user]);
        manager.SetRoles(user, "Admin", "User");
        var signIn = new ConfigurableSignInManager(manager)
        {
            PasswordSignInResult = SignInResult.Success
        };
        var service = new AuthSessionService(
            manager,
            signIn,
            NullLogger<AuthSessionService>.Instance);

        var result = await service.LoginAsync(new LoginRequest
        {
            Email = user.Email!,
            Password = "Password1!"
        }, "trace");

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        signIn.LastUserName.Should().Be(user.UserName);
        result.Value!.GetType().GetProperty("roles")!.GetValue(result.Value)
            .Should().BeEquivalentTo(new[] { "Admin", "User" });
    }
}

public sealed class AuthPasswordServiceTests
{
    [Fact]
    public async Task ForgotPasswordAsync_RejectsMissingEmail()
    {
        var service = Create(new ConfigurableUserManager(), new RecordingEmailService());

        var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest(), "trace");

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ForgotPasswordAsync_SendsEncodedResetLinkForExistingUser()
    {
        var user = TestUsers.Create("person+tag@example.com", "user-1");
        var manager = new ConfigurableUserManager([user])
        {
            PasswordResetToken = "token with spaces/+"
        };
        var email = new RecordingEmailService();
        var service = Create(manager, email);

        var result = await service.ForgotPasswordAsync(
            new ForgotPasswordRequest { Email = user.Email! },
            "trace");

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        email.Messages.Should().ContainSingle();
        email.Messages.Single().Body.Should().Contain("person%2Btag%40example.com");
        email.Messages.Single().Body.Should().Contain("token%20with%20spaces%2F%2B");
        service.GetResetPasswordRedirectUrl("a+b@example.com", "x/y")
            .Should().Contain("a%2Bb%40example.com").And.Contain("x%2Fy");
    }

    private static AuthPasswordService Create(
        ConfigurableUserManager manager,
        RecordingEmailService email) => new(
            manager,
            email,
            TestConfiguration.Create(
                ("Api:Url", "https://api.example/"),
                ("Frontend:Url", "https://studio.example/")),
            NullLogger<AuthPasswordService>.Instance);
}

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterAndRedirect_DelegateToUnderlyingAuthServices()
    {
        var manager = new ConfigurableUserManager();
        var signIn = new ConfigurableSignInManager(manager);
        var configuration = TestConfiguration.Create(("Frontend:Url", "https://studio.example"));
        var service = new AuthService(
            new AuthRegistrationService(manager, configuration, NullLogger<AuthRegistrationService>.Instance),
            new AuthSessionService(manager, signIn, NullLogger<AuthSessionService>.Instance),
            new AuthPasswordService(manager, new RecordingEmailService(), configuration, NullLogger<AuthPasswordService>.Instance));

        var result = await service.RegisterAsync(new RegisterRequest(), "trace");

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        service.GetConfirmEmailRedirectUrl().Should().Be("https://studio.example/identity/login");
    }
}

public sealed class AccountEndpointServiceTests
{
    [Fact]
    public async Task DeleteAccountAsync_RejectsMissingPassword()
    {
        var manager = new ConfigurableUserManager();
        var service = new AccountEndpointService(manager, new ConfigurableSignInManager(manager));

        var result = await service.DeleteAccountAsync(TestUsers.CreatePrincipal(null), " ");

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task DeleteAccountAsync_SignsOutAndDeletesAuthenticatedUser()
    {
        var user = TestUsers.Create("person@example.com", "user-1");
        var manager = new ConfigurableUserManager([user])
        {
            CurrentUser = user,
            CheckPasswordResult = true
        };
        var signIn = new ConfigurableSignInManager(manager);
        var service = new AccountEndpointService(manager, signIn);

        var result = await service.DeleteAccountAsync(
            TestUsers.CreatePrincipal(user),
            "Password1!");

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        signIn.SignedOut.Should().BeTrue();
        manager.Users.Should().BeEmpty();
    }
}

public sealed class PrivacyEndpointServiceTests
{
    [Fact]
    public async Task DeleteAccountAsync_RequiresExplicitConfirmation()
    {
        var manager = new ConfigurableUserManager();
        var service = new PrivacyEndpointService(manager, new StubPrivacyService());

        var result = await service.DeleteAccountAsync(
            TestUsers.CreatePrincipal(null),
            confirmed: false,
            traceId: "trace-1");

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ExportAsync_ReturnsExportForAuthenticatedUser()
    {
        var user = TestUsers.Create("person@example.com", "user-1");
        var manager = new ConfigurableUserManager([user]) { CurrentUser = user };
        var export = new GdprExportResponse(
            DateTime.UtcNow,
            new GdprAccountExport(user.Id, user.Email, user.UserName, null, DateTime.UtcNow, false),
            [], [], [], []);
        var privacy = new StubPrivacyService { ExportResult = export };
        var service = new PrivacyEndpointService(manager, privacy);

        var result = await service.ExportAsync(TestUsers.CreatePrincipal(user), "trace");

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeSameAs(export);
        privacy.LastExportUserId.Should().Be(user.Id);
    }
}

public sealed class ContactRequestServiceTests
{
    [Fact]
    public async Task CreateAsync_RejectsInvalidPhoneNumber()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new ContactRequestService(context);

        var result = await service.CreateAsync(new CreateContactRequestDto
        {
            Name = "Client",
            Email = "client@example.com",
            Phone = "abc"
        });

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await context.ContactRequests.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_TrimsAndPersistsValidRequest()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new ContactRequestService(context);

        var result = await service.CreateAsync(new CreateContactRequestDto
        {
            Name = "  Client  ",
            Email = "  client@example.com  ",
            Phone = " +359 888 123 456 ",
            Subject = "  Session  ",
            Message = "  Details  "
        });

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var stored = await context.ContactRequests.SingleAsync();
        stored.Name.Should().Be("Client");
        stored.Email.Should().Be("client@example.com");
        stored.Subject.Should().Be("Session");
        stored.Message.Should().Be("Details");
        stored.IsSeenByAdmin.Should().BeFalse();
    }
}
