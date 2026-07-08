using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleriesControllerTests
{
    [Fact]
    public async Task ClientGalleries_GetMyGalleries_ReturnsUnauthorized_WhenUserIsMissing()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var controller = CreateClientController(context, user: null);

        var result = await controller.GetMyGalleries();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ClientGalleries_CreateMyGallery_ReturnsBadRequest_WhenTitleIsMissing()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var controller = CreateClientController(context, TestUsers.Create(id: "user-1"));

        var result = await controller.CreateMyGallery(new CreateUserClientGalleryRequest { Title = " " });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ClientGalleries_UploadMyGalleryPhoto_ReturnsBadRequest_WhenFileIsEmpty()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var controller = CreateClientController(context, TestUsers.Create(id: "user-1"));
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
        await using var context = TestDbContextFactory.CreateContext();
        var controller = CreateClientController(context, user: null);

        var result = await controller.GetGalleryDetails(1);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_GetGalleryById_ReturnsBadRequest_WhenIdIsInvalid()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.GetGalleryById(0);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_CreateGallery_ReturnsBadRequest_WhenRequestIsNull()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.CreateGallery(null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_CreateGallery_ReturnsBadRequest_WhenTitleIsMissing()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.CreateGallery(new AdminCreateClientGalleryRequest { Title = " " });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_UpdateGallery_ReturnsBadRequest_WhenIdIsInvalid()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.UpdateGallery(0, new AdminUpdateClientGalleryRequest { Title = "Gallery" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_DeleteGallery_ReturnsBadRequest_WhenIdIsInvalid()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var controller = CreateAdminController(context);

        var result = await controller.DeleteGallery(0);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdminClientGalleries_DownloadAllAlbums_ReturnsNotFound_WhenNoAlbumsExist()
    {
        await using var context = TestDbContextFactory.CreateContext();
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
                    User = TestUsers.CreatePrincipal(user)
                }
            }
        };
    }

    private static AdminClientGalleriesController CreateAdminController(AppDbContext context)
    {
        var user = TestUsers.Create(id: "user-1");
        return new AdminClientGalleriesController(
            null!,
            null!,
            new TestUserManager(user, ["Admin"]),
            context,
            null!,
            NullLogger<AdminClientGalleriesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = TestUsers.CreatePrincipal(user, isAdmin: true)
                }
            }
        };
    }
}
