using System.Security.Claims;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleriesControllerTests
{
    [Fact]
    public async Task ClientGalleries_GetMyGalleries_ReturnsUnauthorized_WhenUserIsMissing()
    {
        await using var context = CreateContext();
        var controller = CreateClientController(context, user: null);

        var result = await controller.GetMyGalleries();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ClientGalleries_CreateMyGallery_ReturnsBadRequest_WhenTitleIsMissing()
    {
        await using var context = CreateContext();
        var controller = CreateClientController(context, CreateUser());

        var result = await controller.CreateMyGallery(new CreateUserClientGalleryRequest { Title = " " });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ClientGalleries_UploadMyGalleryPhoto_ReturnsBadRequest_WhenFileIsEmpty()
    {
        await using var context = CreateContext();
        var controller = CreateClientController(context, CreateUser());
        var file = new FormFile(Stream.Null, 0, 0, "file", "empty.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await controller.UploadMyGalleryPhoto(1, file);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ClientGalleries_GetGalleryDetails_ReturnsUnauthorized_WhenUserIsMissing()
    {
        await using var context = CreateContext();
        var controller = CreateClientController(context, user: null);

        var result = await controller.GetGalleryDetails(1);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_GetGalleryById_ReturnsBadRequest_WhenIdIsInvalid()
    {
        await using var context = CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.GetGalleryById(0);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_CreateGallery_ReturnsBadRequest_WhenRequestIsNull()
    {
        await using var context = CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.CreateGallery(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_CreateGallery_ReturnsBadRequest_WhenTitleIsMissing()
    {
        await using var context = CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.CreateGallery(new AdminCreateClientGalleryRequest { Title = " " });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_UpdateGallery_ReturnsBadRequest_WhenIdIsInvalid()
    {
        await using var context = CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.UpdateGallery(0, new AdminUpdateClientGalleryRequest { Title = "Gallery" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_DeleteGallery_ReturnsBadRequest_WhenIdIsInvalid()
    {
        await using var context = CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.DeleteGallery(0);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_DownloadAllAlbums_ReturnsNotFound_WhenNoAlbumsExist()
    {
        await using var context = CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.DownloadAllAlbums();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    private static ClientGalleriesController CreateClientController(AppDbContext context, ApplicationUser? user)
    {
        return new ClientGalleriesController(
            null!,
            new TestUserManager(user),
            context,
            null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(user)
                }
            }
        };
    }

    private static AdminClientGalleriesController CreateAdminController(AppDbContext context)
    {
        var user = CreateUser();
        return new AdminClientGalleriesController(
            null!,
            null!,
            new TestUserManager(user),
            context,
            null!,
            NullLogger<AdminClientGalleriesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(user, isAdmin: true)
                }
            }
        };
    }

    private static ApplicationUser CreateUser() => new()
    {
        Id = "user-1",
        Email = "user@example.com",
        UserName = "user@example.com"
    };

    private static ClaimsPrincipal CreatePrincipal(ApplicationUser? user, bool isAdmin = false)
    {
        if (user == null)
            return new ClaimsPrincipal(new ClaimsIdentity());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.Email ?? string.Empty)
        };

        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestUserManager : UserManager<ApplicationUser>
    {
        private readonly ApplicationUser? _user;

        public TestUserManager(ApplicationUser? user)
            : base(
                new TestUserStore(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!,
                NullLogger<UserManager<ApplicationUser>>.Instance)
        {
            _user = user;
        }

        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
        {
            return Task.FromResult(_user);
        }
    }

    private sealed class TestUserStore : IUserStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Dispose() { }
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
