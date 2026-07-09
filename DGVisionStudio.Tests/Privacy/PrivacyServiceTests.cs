using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Privacy;

public sealed class PrivacyServiceTests
{
    [Fact]
    public async Task ExportUserDataAsync_ReturnsUserRelatedData()
    {
        await using var context = CreateContext();
        var user = SeedUser(context);
        var album = await SeedOwnedGallery(context, user.Id);
        context.UserAlbumAccesses.Add(new UserAlbumAccess
        {
            UserId = user.Id,
            PortfolioAlbumId = album.Id,
            PreviewEnabled = true,
            DownloadEnabled = true,
            DownloadExpiresAtUtc = DateTime.UtcNow.AddDays(2)
        });
        context.PrintRequests.Add(new PrintRequest
        {
            UserId = user.Id,
            PortfolioAlbumId = album.Id,
            FullName = "Viktor Iliev",
            Email = user.Email!,
            Phone = "+359888000000",
            Notes = "Glossy",
            Items =
            [
                new PrintRequestItem
                {
                    PortfolioImageId = 123,
                    Quantity = 2,
                    Size = "10x15",
                    PaperType = "Glossy",
                    Notes = "Two copies"
                }
            ]
        });
        context.ContactRequests.Add(new ContactRequest
        {
            Id = Guid.NewGuid(),
            Name = "Viktor Iliev",
            Email = user.Email!,
            Phone = "+359888000000",
            Message = "Hello"
        });
        await context.SaveChangesAsync();

        var service = new PrivacyService(context);

        var result = await service.ExportUserDataAsync(user.Id);

        result.Should().NotBeNull();
        result!.Account.Email.Should().Be(user.Email);
        result.OwnedGalleries.Should().ContainSingle(x => x.Id == album.Id && x.ActiveImageCount == 1);
        result.GalleryAccesses.Should().ContainSingle(x => x.PortfolioAlbumId == album.Id && x.DownloadEnabled);
        result.PrintRequests.Should().ContainSingle(x => x.Items.Count == 1 && x.Email == user.Email);
        result.ContactRequests.Should().ContainSingle(x => x.Email == user.Email && x.Message == "Hello");
    }

    [Fact]
    public async Task AnonymizeUserDataAsync_AnonymizesAccountAndRelatedPersonalData()
    {
        await using var context = CreateContext();
        var user = SeedUser(context);
        var album = await SeedOwnedGallery(context, user.Id);
        context.UserAlbumAccesses.Add(new UserAlbumAccess
        {
            UserId = user.Id,
            PortfolioAlbumId = album.Id,
            PreviewEnabled = true,
            DownloadEnabled = true
        });
        context.PrintRequests.Add(new PrintRequest
        {
            UserId = user.Id,
            PortfolioAlbumId = album.Id,
            FullName = "Viktor Iliev",
            Email = user.Email!,
            Phone = "+359888000000",
            Notes = "Private note"
        });
        context.ContactRequests.Add(new ContactRequest
        {
            Id = Guid.NewGuid(),
            Name = "Viktor Iliev",
            Email = user.Email!,
            Phone = "+359888000000",
            Subject = "Private subject",
            Message = "Private message",
            AdminComment = "Private admin note"
        });
        await context.SaveChangesAsync();

        var service = new PrivacyService(context);

        var result = await service.AnonymizeUserDataAsync(user.Id);

        result.Should().BeTrue();

        var anonymizedUser = await context.Users.SingleAsync(x => x.Id == user.Id);
        anonymizedUser.Email.Should().EndWith("@deleted.local");
        anonymizedUser.UserName.Should().Be(anonymizedUser.Email);
        anonymizedUser.PhoneNumber.Should().BeNull();
        anonymizedUser.IsBlocked.Should().BeTrue();

        var anonymizedAlbum = await context.PortfolioAlbums.IgnoreQueryFilters().SingleAsync(x => x.Id == album.Id);
        anonymizedAlbum.OwnerUserId.Should().BeNull();

        (await context.UserAlbumAccesses.CountAsync(x => x.UserId == user.Id)).Should().Be(0);

        var printRequest = await context.PrintRequests.SingleAsync(x => x.UserId == user.Id);
        printRequest.FullName.Should().Be("Deleted user");
        printRequest.Email.Should().Be(anonymizedUser.Email);
        printRequest.Phone.Should().BeNull();
        printRequest.Notes.Should().BeNull();

        var contactRequest = await context.ContactRequests.SingleAsync();
        contactRequest.Name.Should().Be("Deleted user");
        contactRequest.Email.Should().Be(anonymizedUser.Email);
        contactRequest.Phone.Should().BeNull();
        contactRequest.Subject.Should().BeNull();
        contactRequest.Message.Should().Be("Deleted by GDPR request.");
        contactRequest.AdminComment.Should().BeNull();
        contactRequest.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task AnonymizeUserDataAsync_ReturnsFalse_WhenUserDoesNotExist()
    {
        await using var context = CreateContext();
        var service = new PrivacyService(context);

        var result = await service.AnonymizeUserDataAsync("missing-user");

        result.Should().BeFalse();
    }

    private static ApplicationUser SeedUser(AppDbContext context)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = "viktor@example.com",
            NormalizedEmail = "VIKTOR@EXAMPLE.COM",
            UserName = "viktor@example.com",
            NormalizedUserName = "VIKTOR@EXAMPLE.COM",
            PhoneNumber = "+359888000000",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static async Task<PortfolioAlbum> SeedOwnedGallery(AppDbContext context, string userId)
    {
        var category = new PortfolioCategory
        {
            Key = Guid.NewGuid().ToString("N"),
            Name = "Client galleries",
            NameEn = "Client galleries",
            IsActive = true
        };
        context.PortfolioCategories.Add(category);
        await context.SaveChangesAsync();

        var album = new PortfolioAlbum
        {
            PortfolioCategoryId = category.Id,
            Slug = Guid.NewGuid().ToString("N"),
            Title = "Private gallery",
            OwnerUserId = userId,
            IsUserUploaded = true,
            IsPublished = true,
            Images =
            [
                new PortfolioImage
                {
                    ImageUrl = "/uploads/private.jpg",
                    IsPublished = true,
                    IsDeleted = false
                },
                new PortfolioImage
                {
                    ImageUrl = "/uploads/deleted.jpg",
                    IsPublished = true,
                    IsDeleted = true
                }
            ]
        };

        context.PortfolioAlbums.Add(album);
        await context.SaveChangesAsync();
        return album;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
