using System.Reflection;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.DTOs;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Coverage;

public sealed class CloudinaryFileStorageCoverageTests
{
    [Fact]
    public void Constructor_ValidatesEachRequiredCredential()
    {
        var missingKey = () => new CloudinaryFileStorageService(TestConfiguration.Create(
            ("Cloudinary:CloudName", "cloud")));
        var missingSecret = () => new CloudinaryFileStorageService(TestConfiguration.Create(
            ("Cloudinary:CloudName", "cloud"),
            ("Cloudinary:ApiKey", "key")));

        missingKey.Should().Throw<InvalidOperationException>().WithMessage("*ApiKey*");
        missingSecret.Should().Throw<InvalidOperationException>().WithMessage("*ApiSecret*");
    }

    [Fact]
    public async Task EmptyPaths_AreHandledWithoutExternalCloudinaryCalls()
    {
        var service = CreateService();

        (await service.OpenReadAsync(" ")).Should().BeNull();
        (await service.FileExistsAsync(" ")).Should().BeFalse();
        await service.DeleteFileAsync(" ");

        var unsupported = () => service.SaveFileAsync(
            new MemoryStream([1, 2, 3]),
            "payload.exe",
            "uploads/portfolio");
        await unsupported.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unsupported image format.");
    }

    [Theory]
    [InlineData("uploads/portfolio/albums", "portfolio/albums")]
    [InlineData("/uploads/portfolio/albums/", "portfolio/albums")]
    [InlineData("https://example.com/uploads/portfolio/albums", "portfolio/albums")]
    [InlineData("uploads\\portfolio\\albums", "portfolio/albums")]
    [InlineData(" ", "")]
    public void NormalizeCloudinaryFolder_HandlesUrlsSlashesAndEmptyValues(string input, string expected)
    {
        InvokeStatic<string>("NormalizeCloudinaryFolder", input).Should().Be(expected);
    }

    [Theory]
    [InlineData("  My.File_Name  ", "my-file-name")]
    [InlineData("---", null)]
    public void SanitizePublicId_NormalizesNamesAndFallsBackForEmptyResults(string input, string? expected)
    {
        var result = InvokeStatic<string>("SanitizePublicId", input);

        if (expected is null)
            result.Should().MatchRegex("^[a-f0-9]{32}$");
        else
            result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://res.cloudinary.com/demo/image/upload/v123/folder/photo.jpg", "folder/photo")]
    [InlineData("folder/photo.webp", "folder/photo")]
    [InlineData("folder/document.txt", "folder/document.txt")]
    public void ExtractPublicId_StripsCloudinaryUploadPrefixVersionAndKnownExtensions(
        string input,
        string expected)
    {
        var service = CreateService();
        var method = typeof(CloudinaryFileStorageService).GetMethod(
            "ExtractPublicId",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        method.Invoke(service, [input]).Should().Be(expected);
    }

    [Fact]
    public async Task OversizedUpload_NormalizesAbsoluteFolderWithoutCallingCloudinary()
    {
        var service = CreateService();
        await using var stream = new MemoryStream(new byte[10 * 1024 * 1024 + 1]);

        var path = await service.SaveImageAsync(
            stream,
            "Photo.JPG",
            "https://example.com/uploads/portfolio/client/");

        path.Should().Be("portfolio/client/Photo.JPG");
    }

    private static CloudinaryFileStorageService CreateService() => new(
        TestConfiguration.Create(
            ("Cloudinary:CloudName", "test-cloud"),
            ("Cloudinary:ApiKey", "test-key"),
            ("Cloudinary:ApiSecret", "test-secret"),
            ("Cloudinary:Folder", " /dgvisionstudio/portfolio/ ")));

    private static T InvokeStatic<T>(string methodName, params object?[] arguments)
    {
        var method = typeof(CloudinaryFileStorageService).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return (T)method.Invoke(null, arguments)!;
    }
}

public sealed class PortfolioCategoryAdminCoverageTests
{
    [Fact]
    public async Task CreateAndUpdate_RejectDuplicateKeysAndNormalizeSuccessfulUpdate()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var existing = Category("existing", 1);
        var updated = Category("updated", 2);
        fixture.Context.AddRange(existing, updated);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = Create(fixture.Context, audit);

        var duplicateCreate = await service.CreateCategoryAsync(
            new CreatePortfolioCategoryRequest
            {
                Key = " EXISTING ",
                Name = "Duplicate",
                NameEn = "Duplicate"
            },
            Context());
        var duplicateUpdate = await service.UpdateCategoryAsync(
            updated.Id,
            new UpdatePortfolioCategoryRequest
            {
                Key = " EXISTING ",
                Name = "Updated",
                NameEn = "Updated"
            },
            Context());
        var validUpdate = await service.UpdateCategoryAsync(
            updated.Id,
            new UpdatePortfolioCategoryRequest
            {
                Key = " NEW-KEY ",
                Name = "  Ново име  ",
                NameEn = "  New name  ",
                Description = "  Description  ",
                DisplayOrder = 0,
                IsActive = true
            },
            Context());

        duplicateCreate.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        duplicateUpdate.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        validUpdate.StatusCode.Should().Be(StatusCodes.Status200OK);
        updated.Key.Should().Be("new-key");
        updated.Name.Should().Be("Ново име");
        updated.NameEn.Should().Be("New name");
        updated.Description.Should().Be("Description");
        updated.DisplayOrder.Should().Be(1);
        audit.Entries.Should().ContainSingle(x => x.Action == "UpdatePortfolioCategory");
    }

    [Fact]
    public async Task MoveCategoryAsync_ReordersEveryCategoryAndAuditsChange()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var first = Category("first", 1);
        var second = Category("second", 2);
        var third = Category("third", 3);
        fixture.Context.AddRange(first, second, third);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = Create(fixture.Context, audit);

        var result = await service.MoveCategoryAsync(
            third.Id,
            new MovePortfolioCategoryRequest { DisplayOrder = 1 },
            Context());

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        var ordered = await fixture.Context.PortfolioCategories.OrderBy(x => x.DisplayOrder).ToListAsync();
        ordered.Select(x => x.Id).Should().Equal(third.Id, first.Id, second.Id);
        ordered.Select(x => x.DisplayOrder).Should().Equal(1, 2, 3);
        audit.Entries.Should().ContainSingle(x => x.Action == "MovePortfolioCategory");
    }

    [Fact]
    public async Task CategoryAlbumMethods_ReturnSelectionAndAssignRequestedNonUserAlbums()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = Category("target", 1);
        var other = Category("other", 2);
        var selected = Album(other, "selected", userUploaded: false);
        var unselected = Album(other, "unselected", userUploaded: false);
        var userUpload = Album(other, "user-upload", userUploaded: true);
        fixture.Context.AddRange(category, other, selected, unselected, userUpload);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = Create(fixture.Context, audit);

        var before = await service.GetCategoryAlbumsAsync(category.Id);
        var updated = await service.UpdateCategoryAlbumsAsync(
            category.Id,
            new UpdateCategoryAlbumsRequest { AlbumIds = [selected.Id, selected.Id] },
            Context());
        var missing = await service.GetCategoryAlbumsAsync(9999);

        before.StatusCode.Should().Be(StatusCodes.Status200OK);
        updated.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        selected.PortfolioCategoryId.Should().Be(category.Id);
        unselected.PortfolioCategoryId.Should().Be(other.Id);
        userUpload.PortfolioCategoryId.Should().Be(other.Id);
        audit.Entries.Should().ContainSingle(x => x.Action == "UpdatePortfolioCategoryAlbums");
    }

    [Fact]
    public async Task DeleteCategoryAsync_SoftDeletesCategoryAlbumsAndImagesThenNormalizesOrder()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var deleted = Category("delete", 1);
        var remaining = Category("remaining", 4);
        var album = Album(deleted, "album", userUploaded: false);
        var image = new PortfolioImage
        {
            PortfolioAlbum = album,
            ImageUrl = "/photo.jpg",
            IsPublished = true,
            IsCover = true
        };
        fixture.Context.AddRange(deleted, remaining, album, image);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = Create(fixture.Context, audit);

        var result = await service.DeleteCategoryAsync(deleted.Id, Context());

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        deleted.IsDeleted.Should().BeTrue();
        deleted.IsActive.Should().BeFalse();
        album.IsDeleted.Should().BeTrue();
        album.IsPublished.Should().BeFalse();
        album.AllowClientAccess.Should().BeFalse();
        image.IsDeleted.Should().BeTrue();
        image.IsPublished.Should().BeFalse();
        image.IsCover.Should().BeFalse();
        remaining.DisplayOrder.Should().Be(2);
        audit.Entries.Should().ContainSingle(x => x.Action == "SoftDeletePortfolioCategory");
    }

    private static PortfolioCategoryAdminService Create(
        DGVisionStudio.Infrastructure.Data.AppDbContext context,
        RecordingAuditLogService audit) =>
        new(context, audit, NullLogger<PortfolioCategoryAdminService>.Instance);

    private static PortfolioCategory Category(string key, int order) => new()
    {
        Key = key,
        Name = key,
        NameEn = key,
        DisplayOrder = order,
        IsActive = true
    };

    private static PortfolioAlbum Album(PortfolioCategory category, string slug, bool userUploaded) => new()
    {
        PortfolioCategory = category,
        Slug = slug,
        Title = slug,
        DisplayOrder = 1,
        IsPublished = true,
        AllowClientAccess = true,
        IsUserUploaded = userUploaded
    };

    private static AdminRequestContext Context() =>
        new("admin", "admin@example.com", "Admin", "127.0.0.1", "tests", "trace");
}

public sealed class AdminUserCoverageTests
{
    [Fact]
    public async Task GetUsersAndMarkAllSeen_HandlePagingRolesProtectionAndBlockedUsers()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var protectedUser = TestUsers.Create("protected@example.com", "protected");
        protectedUser.CreatedAtUtc = DateTime.UtcNow;
        protectedUser.IsSeenByAdmin = false;
        var blocked = TestUsers.Create("blocked@example.com", "blocked");
        blocked.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        blocked.IsSeenByAdmin = false;
        blocked.IsBlocked = true;
        fixture.Context.Users.AddRange(protectedUser, blocked);
        await fixture.Context.SaveChangesAsync();
        var manager = new ConfigurableUserManager([protectedUser, blocked])
        {
            UsersSource = fixture.Context.Users
        };
        manager.SetRoles(protectedUser, "Admin", "User");
        var service = new AdminUserService(
            manager,
            fixture.Context,
            TestConfiguration.Create(("Seed:PrimaryAdminEmail", "protected@example.com")));

        var users = await service.GetUsersAsync(new PagedQueryDto { Page = 1, PageSize = 1 });
        var marked = await service.MarkAllSeenAsync();

        users.StatusCode.Should().Be(StatusCodes.Status200OK);
        var page = users.Value.Should().BeOfType<PagedResultDto<object>>().Subject;
        page.Total.Should().Be(2);
        page.Items.Should().ContainSingle();
        marked.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        protectedUser.IsSeenByAdmin.Should().BeTrue();
        blocked.IsSeenByAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserAlbumsAsync_ReturnsAssignedAndAvailableGalleries()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var user = TestUsers.Create("client@example.com", "client");
        var category = new PortfolioCategory { Key = "client", Name = "Client", NameEn = "Client" };
        var assigned = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "assigned",
            Title = "Assigned",
            AllowClientAccess = true,
            IsUserUploaded = false
        };
        var available = new PortfolioAlbum
        {
            PortfolioCategory = category,
            Slug = "available",
            Title = "Available",
            AllowClientAccess = true,
            IsUserUploaded = false
        };
        fixture.Context.AddRange(user, category, assigned, available);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.UserAlbumAccesses.Add(new UserAlbumAccess
        {
            UserId = user.Id,
            PortfolioAlbumId = assigned.Id,
            PreviewEnabled = true,
            DownloadEnabled = false
        });
        await fixture.Context.SaveChangesAsync();
        var manager = new ConfigurableUserManager([user]) { UsersSource = fixture.Context.Users };
        manager.SetRoles(user, "User");
        var service = new AdminUserService(manager, fixture.Context, TestConfiguration.Create());

        var result = await service.GetUserAlbumsAsync(user.Id);
        var missing = await service.GetUserAlbumsAsync("missing");

        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var value = result.Value!;
        var accesses = (System.Collections.IEnumerable)value.GetType().GetProperty("accesses")!.GetValue(value)!;
        var availableGalleries = (System.Collections.IEnumerable)value.GetType().GetProperty("availableGalleries")!.GetValue(value)!;
        accesses.Cast<object>().Should().ContainSingle();
        availableGalleries.Cast<object>().Should().ContainSingle();
    }

    [Fact]
    public async Task RoleBlockUnblockAndDeleteCommands_CoverSuccessProtectionAndIdentityFailure()
    {
        var protectedUser = TestUsers.Create("protected@example.com", "protected");
        var normal = TestUsers.Create("normal@example.com", "normal");
        var failing = TestUsers.Create("failing@example.com", "failing");
        var manager = new ConfigurableUserManager([protectedUser, normal, failing]);
        await using var context = TestDbContextFactory.CreateContext();
        var service = new AdminUserService(
            manager,
            context,
            TestConfiguration.Create(("Seed:PrimaryAdminEmail", protectedUser.Email)));

        var madeAdmin = await service.MakeAdminAsync(normal.Id);
        var removed = await service.RemoveAdminAsync(normal.Id);
        var protectedRemoval = await service.RemoveAdminAsync(protectedUser.Id);
        var blocked = await service.BlockUserAsync(normal.Id);
        var unblocked = await service.UnblockUserAsync(normal.Id);
        var missing = await service.BlockUserAsync("missing");
        manager.DeleteResult = IdentityResult.Failed(new IdentityError { Code = "DeleteFailed" });
        var failedDelete = await service.DeleteUserAsync(failing.Id);
        manager.DeleteResult = IdentityResult.Success;
        var deleted = await service.DeleteUserAsync(normal.Id);

        madeAdmin.StatusCode.Should().Be(StatusCodes.Status200OK);
        manager.AddedRoles.Should().Contain((normal.Id, "Admin"));
        manager.AddedRoles.Should().Contain((normal.Id, "User"));
        removed.StatusCode.Should().Be(StatusCodes.Status200OK);
        manager.RemovedRoles.Should().Contain((normal.Id, "Admin"));
        protectedRemoval.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        blocked.StatusCode.Should().Be(StatusCodes.Status200OK);
        unblocked.StatusCode.Should().Be(StatusCodes.Status200OK);
        normal.IsBlocked.Should().BeFalse();
        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        failedDelete.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        deleted.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }
}
