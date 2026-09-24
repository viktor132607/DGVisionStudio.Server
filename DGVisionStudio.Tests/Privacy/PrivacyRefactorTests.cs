using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Tests.Privacy;

public sealed class PrivacyRefactorTests
{
    [Fact]
    public void ExportMapper_MapsAccountGalleryAccessAndNestedPrintItems()
    {
        var mapper = new PrivacyExportMapper();
        var user = User();
        var album = new PortfolioAlbum
        {
            Id = 7,
            Title = "Gallery",
            Slug = "gallery",
            Images =
            [
                new PortfolioImage { IsDeleted = false },
                new PortfolioImage { IsDeleted = true }
            ]
        };
        var access = new UserAlbumAccess
        {
            PortfolioAlbumId = album.Id,
            PortfolioAlbum = album,
            PreviewEnabled = true,
            DownloadEnabled = true
        };
        var request = new PrintRequest
        {
            Id = 11,
            PortfolioAlbumId = album.Id,
            FullName = "Client",
            Email = user.Email!,
            Status = "New",
            Items =
            [
                new PrintRequestItem
                {
                    Id = 3,
                    PortfolioImageId = 44,
                    Quantity = 2,
                    Size = "10x15"
                }
            ]
        };

        mapper.MapAccount(user).Email.Should().Be(user.Email);
        mapper.MapOwnedGallery(album).ActiveImageCount.Should().Be(1);
        mapper.MapGalleryAccess(access).PortfolioAlbumTitle.Should().Be("Gallery");
        mapper.MapPrintRequest(request).Items.Should().ContainSingle(
            x => x.PortfolioImageId == 44 && x.Quantity == 2);
    }

    [Fact]
    public void AnonymizationMapper_RemovesPersonalDataFromAccountAndRequests()
    {
        var mapper = new PrivacyAnonymizationMapper();
        var user = User();
        var print = new PrintRequest
        {
            FullName = "Client",
            Email = user.Email!,
            Phone = "123",
            Notes = "Private"
        };
        var contact = new ContactRequest
        {
            Id = Guid.NewGuid(),
            Name = "Client",
            Email = user.Email!,
            Phone = "123",
            Subject = "Private",
            Message = "Private",
            AdminComment = "Private"
        };

        var anonymizedEmail = mapper.AnonymizeAccount(user);
        mapper.AnonymizePrintRequest(print, anonymizedEmail);
        mapper.AnonymizeContactRequest(contact, anonymizedEmail);

        anonymizedEmail.Should().EndWith("@deleted.local");
        user.Email.Should().Be(anonymizedEmail);
        user.PhoneNumber.Should().BeNull();
        user.IsBlocked.Should().BeTrue();
        user.TwoFactorEnabled.Should().BeFalse();

        print.FullName.Should().Be("Deleted user");
        print.Email.Should().Be(anonymizedEmail);
        print.Phone.Should().BeNull();
        print.Notes.Should().BeNull();

        contact.Name.Should().Be("Deleted user");
        contact.Email.Should().Be(anonymizedEmail);
        contact.Message.Should().Be("Deleted by GDPR request.");
        contact.Subject.Should().BeNull();
        contact.AdminComment.Should().BeNull();
        contact.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task ExportQuery_ReturnsNullForMissingUser()
    {
        await using var context = CreateContext();
        var service = new PrivacyExportQueryService(
            context,
            new PrivacyExportMapper());

        var result = await service.ExportUserDataAsync("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AnonymizationService_ReturnsFalseForMissingUser()
    {
        await using var context = CreateContext();
        var service = new PrivacyAnonymizationService(
            context,
            new PrivacyAnonymizationMapper());

        var result = await service.AnonymizeUserDataAsync("missing");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Facade_DelegatesFocusedExportAndAnonymizationServices()
    {
        await using var context = CreateContext();
        var user = User();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var service = new PrivacyService(
            new PrivacyExportQueryService(context, new PrivacyExportMapper()),
            new PrivacyAnonymizationService(
                context,
                new PrivacyAnonymizationMapper()));

        var export = await service.ExportUserDataAsync(user.Id);
        var anonymized = await service.AnonymizeUserDataAsync(user.Id);

        export.Should().NotBeNull();
        anonymized.Should().BeTrue();
        user.Email.Should().EndWith("@deleted.local");
    }

    private static ApplicationUser User() =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            Email = "person@example.com",
            NormalizedEmail = "PERSON@EXAMPLE.COM",
            UserName = "person@example.com",
            NormalizedUserName = "PERSON@EXAMPLE.COM",
            PhoneNumber = "123",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
        };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
