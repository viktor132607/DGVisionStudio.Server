using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs.PrintRequests;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.PrintRequests;

public sealed class ClientPrintRequestEndpointRefactorTests
{
    [Fact]
    public void UserContext_ReturnsNameIdentifierOrNull()
    {
        var service = new ClientPrintRequestUserContextService();

        service.GetUserId(TestUsers.CreatePrincipal(null))
            .Should().BeNull();

        service.GetUserId(
                TestUsers.CreatePrincipal(
                    TestUsers.Create("client@example.com", "user-1")))
            .Should().Be("user-1");
    }

    [Fact]
    public void Mapper_PreservesDtoFallbacksAndCreateNormalization()
    {
        var mapper = new ClientPrintRequestMapper();
        var mapped = mapper.Map(new PrintRequest
        {
            Id = 7,
            UserId = "user-1",
            PortfolioAlbumId = 3,
            FullName = "Client",
            Email = "client@example.com",
            Items =
            {
                new PrintRequestItem
                {
                    Id = 9,
                    PortfolioImageId = 11,
                    Quantity = 2,
                    Size = "10x15"
                }
            }
        });

        mapped.UserEmail.Should().BeEmpty();
        mapped.AlbumTitle.Should().BeEmpty();
        mapped.Items.Should().ContainSingle();
        mapped.Items[0].ImageUrl.Should().BeEmpty();

        var created = mapper.Create(
            "user-1",
            new CreatePrintRequestDto
            {
                PortfolioAlbumId = 4,
                FullName = "  Client  ",
                Email = "  client@example.com  ",
                Phone = "  123  ",
                Notes = "  note  ",
                Items =
                {
                    new CreatePrintRequestItemDto
                    {
                        PortfolioImageId = 15,
                        Quantity = 0,
                        Size = "  20x30  ",
                        PaperType = "  Matte  ",
                        Notes = "  item note  "
                    }
                }
            });

        created.FullName.Should().Be("Client");
        created.Email.Should().Be("client@example.com");
        created.Phone.Should().Be("123");
        created.Notes.Should().Be("note");
        created.Status.Should().Be("New");
        created.IsSeenByAdmin.Should().BeFalse();
        created.Items.Single().Quantity.Should().Be(1);
        created.Items.Single().Size.Should().Be("20x30");
        created.Items.Single().PaperType.Should().Be("Matte");
        created.Items.Single().Notes.Should().Be("item note");
    }

    [Fact]
    public async Task QueryService_ReturnsOnlyCurrentUsersRequestsInDescendingOrder()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var user = TestUsers.Create("client@example.com", "user-1");
        var otherUser = TestUsers.Create("other@example.com", "user-2");
        var category = new PortfolioCategory
        {
            Key = "query-tests",
            Name = "Query Tests",
            NameEn = "Query Tests"
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "query-album",
            Title = "Query Album"
        };
        var mineOld = Request(user, "old@example.com", album);
        mineOld.CreatedAtUtc = new DateTime(
            2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var mineNew = Request(user, "new@example.com", album);
        mineNew.CreatedAtUtc = new DateTime(
            2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var other = Request(otherUser, "other@example.com", album);
        other.CreatedAtUtc = new DateTime(
            2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        context.AddRange(
            user,
            otherUser,
            category,
            album,
            mineOld,
            mineNew,
            other);
        await context.SaveChangesAsync();

        var service = new ClientPrintRequestQueryService(
            context,
            new ClientPrintRequestUserContextService(),
            new ClientPrintRequestMapper());

        var result = await service.GetMineAsync(
            TestUsers.CreatePrincipal(user));

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should()
            .BeOfType<List<PrintRequestDto>>()
            .Which.Select(item => item.Email)
            .Should().Equal(
                "new@example.com",
                "old@example.com");
    }

    [Fact]
    public async Task CreationService_PreservesAccessImageValidationAndPersistence()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var user = TestUsers.Create("client@example.com", "user-1");
        var category = new PortfolioCategory
        {
            Key = "prints",
            Name = "Prints",
            NameEn = "Prints"
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "print-album",
            Title = "Print Album"
        };
        var image = new PortfolioImage
        {
            PortfolioAlbum = album,
            ImageUrl = "/photo.jpg"
        };
        var access = new UserAlbumAccess
        {
            User = user,
            UserId = user.Id,
            PortfolioAlbum = album,
            PreviewEnabled = true
        };
        context.AddRange(user, category, album, image, access);
        await context.SaveChangesAsync();

        var service = new ClientPrintRequestCreationService(
            context,
            new ClientPrintRequestUserContextService(),
            new ClientPrintRequestMapper());
        var principal = TestUsers.CreatePrincipal(user);

        var invalid = await service.CreateAsync(
            principal,
            new CreatePrintRequestDto
            {
                PortfolioAlbumId = album.Id,
                FullName = "Client",
                Email = "client@example.com",
                Items =
                {
                    new CreatePrintRequestItemDto
                    {
                        PortfolioImageId = image.Id + 999,
                        Size = "10x15"
                    }
                }
            });

        invalid.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var created = await service.CreateAsync(
            principal,
            new CreatePrintRequestDto
            {
                PortfolioAlbumId = album.Id,
                FullName = " Client ",
                Email = " client@example.com ",
                Items =
                {
                    new CreatePrintRequestItemDto
                    {
                        PortfolioImageId = image.Id,
                        Quantity = 0,
                        Size = " 10x15 "
                    }
                }
            });

        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var stored = await context.PrintRequests
            .Include(request => request.Items)
            .SingleAsync();
        stored.UserId.Should().Be("user-1");
        stored.FullName.Should().Be("Client");
        stored.Email.Should().Be("client@example.com");
        stored.Items.Should().ContainSingle();
        stored.Items.Single().Quantity.Should().Be(1);
        stored.Items.Single().Size.Should().Be("10x15");
    }

    [Fact]
    public async Task FacadeCompatibilityConstructor_PreservesUnauthorizedValidation()
    {
        await using var context = TestDbContextFactory.CreateContext();
        var service = new ClientPrintRequestEndpointService(context);

        var result = await service.GetMineAsync(
            TestUsers.CreatePrincipal(null));

        result.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    private static PrintRequest Request(
        ApplicationUser user,
        string email,
        PortfolioAlbum album) =>
        new()
        {
            User = user,
            UserId = user.Id,
            PortfolioAlbum = album,
            FullName = "Client",
            Email = email,
            Status = "New"
        };
}
