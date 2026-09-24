using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.ClientGalleries;

public sealed class ClientGalleryAdminCommandRefactorTests
{
    [Fact]
    public void Normalizer_TrimsFieldsAndNormalizesUnsupportedTypeAndStatus()
    {
        var normalizer = new ClientGalleryAdminInputNormalizer();

        var input = normalizer.Normalize(new AdminCreateClientGalleryRequest
        {
            Title = "  Gallery  ",
            TitleEn = "  Gallery EN  ",
            Description = " ",
            CoverImageUrl = "  /cover.jpg  ",
            GalleryType = (GalleryType)999,
            UserGalleryStatus = UserClientGalleryStatus.Processed,
            IsPublic = true,
            IsPublished = true
        });

        input.Title.Should().Be("Gallery");
        input.TitleEn.Should().Be("Gallery EN");
        input.Description.Should().BeNull();
        input.CoverImageUrl.Should().Be("/cover.jpg");
        input.GalleryType.Should().Be(GalleryType.Photoshoot);
        input.UserGalleryStatus.Should().Be(UserClientGalleryStatus.PhotoshootUploaded);
    }

    [Fact]
    public async Task CategoryService_UsesRequestedPublicCategoryAndCalculatesNextOrder()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = new PortfolioCategory
        {
            Key = "public",
            Name = "Public",
            NameEn = "Public",
            IsActive = true
        };
        fixture.Context.PortfolioCategories.Add(category);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.PortfolioAlbums.Add(new PortfolioAlbum
        {
            PortfolioCategoryId = category.Id,
            Slug = "existing",
            Title = "Existing",
            DisplayOrder = 4
        });
        await fixture.Context.SaveChangesAsync();

        var service = new ClientGalleryAdminCategoryService(
            fixture.Context,
            new ClientGalleryNamingService(fixture.Context));
        var input = new ClientGalleryAdminInput(
            "Gallery",
            null,
            null,
            null,
            true,
            true,
            true,
            category.Id,
            GalleryType.Photoshoot,
            UserClientGalleryStatus.PhotoshootUploaded,
            []);

        (await service.ResolveCreateCategoryAsync(input)).Should().Be(category.Id);
        (await service.GetNextDisplayOrderAsync(category.Id)).Should().Be(5);
    }

    [Fact]
    public async Task UpdateService_RebuildsSlugAndResetsPhotoshootOwnershipState()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = new PortfolioCategory
        {
            Key = "gallery",
            Name = "Gallery",
            NameEn = "Gallery",
            IsActive = true
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "old-title",
            Title = "Old title",
            TitleEn = "Old title",
            GalleryType = GalleryType.ClientPrintUpload,
            IsUserUploaded = true,
            OwnerUserId = "owner-1",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
        };
        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();

        var access = new StubClientGalleryAccessService();
        var naming = new ClientGalleryNamingService(fixture.Context);
        var normalizer = new ClientGalleryAdminInputNormalizer();
        var categoryService = new ClientGalleryAdminCategoryService(
            fixture.Context,
            naming);
        var mapper = new ClientGalleryAdminAlbumMapper();
        var service = new ClientGalleryAdminUpdateService(
            fixture.Context,
            access,
            naming,
            normalizer,
            categoryService,
            mapper,
            NullLogger<ClientGalleryAdminUpdateService>.Instance);

        var updated = await service.UpdateGalleryAsync(
            album.Id,
            new AdminUpdateClientGalleryRequest
            {
                Title = "New title",
                TitleEn = "New title",
                IsActive = true,
                IsPublic = true,
                IsPublished = true,
                PortfolioCategoryId = category.Id,
                GalleryType = GalleryType.Photoshoot,
                UserGalleryStatus = UserClientGalleryStatus.PhotoshootReadyForPickup
            });

        updated.Should().BeTrue();
        album.Slug.Should().Be("new-title");
        album.GalleryType.Should().Be(GalleryType.Photoshoot);
        album.IsUserUploaded.Should().BeFalse();
        album.OwnerUserId.Should().BeNull();
        album.ExpiresAtUtc.Should().BeNull();
        album.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task LifecycleService_SoftDeletesGalleryAndImages()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = new PortfolioCategory
        {
            Key = "gallery",
            Name = "Gallery",
            NameEn = "Gallery",
            IsActive = true
        };
        var album = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "gallery",
            Title = "Gallery",
            IsPublished = true,
            AllowClientAccess = true
        };
        album.Images.Add(new PortfolioImage
        {
            ImageUrl = "/photo.jpg",
            IsPublished = true,
            IsCover = true
        });
        fixture.Context.AddRange(category, album);
        await fixture.Context.SaveChangesAsync();

        var service = new ClientGalleryAdminLifecycleService(
            fixture.Context,
            NullLogger<ClientGalleryAdminLifecycleService>.Instance);

        var deleted = await service.DeleteGalleryAsync(album.Id);

        deleted.Should().BeTrue();
        album.IsDeleted.Should().BeTrue();
        album.IsPublished.Should().BeFalse();
        album.AllowClientAccess.Should().BeFalse();
        album.DeletedAtUtc.Should().NotBeNull();
        album.Images.Should().OnlyContain(
            image => image.IsDeleted &&
                     !image.IsPublished &&
                     !image.IsCover &&
                     image.DeletedAtUtc != null);
    }

    [Fact]
    public async Task CommandFacade_DelegatesCreateUpdateAndDelete()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var access = new StubClientGalleryAccessService();
        var naming = new ClientGalleryNamingService(fixture.Context);
        var normalizer = new ClientGalleryAdminInputNormalizer();
        var categories = new ClientGalleryAdminCategoryService(
            fixture.Context,
            naming);
        var mapper = new ClientGalleryAdminAlbumMapper();

        var facade = new ClientGalleryAdminCommandService(
            new ClientGalleryAdminCreateService(
                fixture.Context,
                access,
                naming,
                normalizer,
                categories,
                mapper,
                NullLogger<ClientGalleryAdminCreateService>.Instance),
            new ClientGalleryAdminUpdateService(
                fixture.Context,
                access,
                naming,
                normalizer,
                categories,
                mapper,
                NullLogger<ClientGalleryAdminUpdateService>.Instance),
            new ClientGalleryAdminLifecycleService(
                fixture.Context,
                NullLogger<ClientGalleryAdminLifecycleService>.Instance));

        var id = await facade.CreateGalleryAsync(new AdminCreateClientGalleryRequest
        {
            Title = "Gallery",
            IsActive = true,
            IsPublic = false
        });

        var updated = await facade.UpdateGalleryAsync(
            id,
            new AdminUpdateClientGalleryRequest
            {
                Title = "Updated gallery",
                IsActive = true,
                IsPublic = false,
                GalleryType = GalleryType.Photoshoot,
                UserGalleryStatus = UserClientGalleryStatus.PhotoshootUploaded
            });

        var deleted = await facade.DeleteGalleryAsync(id);

        id.Should().BeGreaterThan(0);
        updated.Should().BeTrue();
        deleted.Should().BeTrue();

        var album = await fixture.Context.PortfolioAlbums
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == id);
        album.Title.Should().Be("Updated gallery");
        album.IsDeleted.Should().BeTrue();
    }
}
