using System.IO.Compression;
using System.Reflection;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class PortfolioAlbumManagementTests
{
    private static readonly AdminRequestContext Admin = new("admin", "admin@test.bg", "Admin", null, "test", "trace");

    private static PortfolioAlbum Album(PortfolioCategory category, string title, string? imageUrl = null)
    {
        var album = new PortfolioAlbum { PortfolioCategory = category, Title = title, Slug = Guid.NewGuid().ToString("N") };
        if (imageUrl is not null) album.Images.Add(new PortfolioImage { ImageUrl = imageUrl, Name = "Снимка.jpg" });
        return album;
    }

    private static PortfolioCategory Category(string name, bool active = true) => new()
    { Name = name, Key = Guid.NewGuid().ToString("N"), IsActive = active };

    [Fact]
    public async Task AllArchive_PreservesHierarchyAndBytes_OnlyFromActiveCategories()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var active = Category("Сватби");
        var album = Album(active, "Анна и Иван", "/uploads/a.jpg");
        album.IsPublished = false; // Publication is independent of category activity.
        album.Images.First().IsPublished = false;
        var deletedPhoto = new PortfolioImage { ImageUrl = "/uploads/deleted.jpg", IsDeleted = true };
        album.Images.Add(deletedPhoto);
        fixture.Context.AddRange(album, Album(active, "Празен албум"), Category("Празна категория"),
            Album(Category("Скрити", false), "Не включвай", "/uploads/inactive.jpg"),
            Album(new PortfolioCategory { Key = "deleted", Name = "Deleted", IsDeleted = true }, "Deleted category album"));
        var userAlbum = Album(active, "User gallery", "/uploads/user.jpg");
        userAlbum.IsUserUploaded = true;
        fixture.Context.Add(userAlbum);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();
        var storage = new StubFileStorageService();
        storage.Files["/uploads/a.jpg"] = [7, 8, 9, 10];
        var events = new List<ArchiveProgress>();
        var archive = await new PortfolioArchiveBuilder(fixture.Context, storage).BuildAsync(null, events.Add, default);
        try
        {
            using var zip = ZipFile.OpenRead(archive.Path);
            var root = $"Archive({DateTime.UtcNow:yyyy-MM-dd})/";
            zip.Entries.Select(e => e.FullName).Should().BeEquivalentTo(
                root, root + "Сватби/", root + "Сватби/Анна и Иван/", root + "Сватби/Анна и Иван/Снимка.jpg",
                root + "Сватби/Празен албум/", root + "Празна категория/");
            await using var content = zip.GetEntry(root + "Сватби/Анна и Иван/Снимка.jpg")!.Open();
            using var bytes = new MemoryStream();
            await content.CopyToAsync(bytes);
            bytes.ToArray().Should().Equal(storage.Files["/uploads/a.jpg"]);
            events.Last().Should().Be(new ArchiveProgress("verifying", 1, 1));
        }
        finally { await archive.CleanupAsync(); }
    }

    [Fact]
    public async Task SelectedArchive_OnlyExportsSelectedAlbums_AndDisambiguatesUnsafeNames()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var first = Album(Category("Cat/Name"), "CON", "/uploads/a.jpg");
        first.Images.Add(new PortfolioImage { ImageUrl = "/uploads/b.jpg", Name = "снимка.JPG" });
        var sameCategory = Album(first.PortfolioCategory!, "CON", "/uploads/c.jpg");
        var secondCategory = Album(Category("Cat\\Name"), "..", "/uploads/d.jpg");
        var unselected = Album(first.PortfolioCategory!, "Unselected", "/uploads/missing.jpg");
        fixture.Context.AddRange(first, sameCategory, secondCategory, unselected);
        await fixture.Context.SaveChangesAsync();
        var storage = new StubFileStorageService();
        foreach (var (url, value) in new[] { ("a", (byte)1), ("b", (byte)2), ("c", (byte)3), ("d", (byte)4) })
            storage.Files[$"/uploads/{url}.jpg"] = [value];
        var archive = await new PortfolioArchiveBuilder(fixture.Context, storage)
            .BuildAsync([first.Id, first.Id, sameCategory.Id, secondCategory.Id], null, default);
        try
        {
            using var zip = ZipFile.OpenRead(archive.Path);
            zip.Entries.Select(e => e.FullName.ToUpperInvariant()).Should().OnlyHaveUniqueItems();
            var files = zip.Entries.Where(e => e.Name.Length > 0).ToArray();
            files.Should().HaveCount(4);
            files.Should().OnlyContain(e => e.FullName.Split('/').Length == 4 && !e.FullName.Contains("..") && !e.FullName.Contains("Unselected"));
            files.Select(e => e.FullName.Split('/')[1]).Distinct().Should().HaveCount(2);
            files.Should().OnlyContain(e => !e.FullName.EndsWith(".jpg.jpg"));
            var contents = new List<byte>();
            foreach (var file in files) { using var stream = file.Open(); contents.Add((byte)stream.ReadByte()); }
            contents.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });
        }
        finally { await archive.CleanupAsync(); }
    }

    [Fact]
    public async Task Archive_RejectsMissingPhoto_EvenWhenOtherPhotosAreAvailable()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var album = Album(Category("Active"), "Album", "/uploads/available.jpg");
        album.Images.Add(new PortfolioImage { ImageUrl = "/uploads/missing.jpg" });
        fixture.Context.Add(album);
        await fixture.Context.SaveChangesAsync();
        var storage = new StubFileStorageService();
        storage.Files["/uploads/available.jpg"] = [1];
        var build = () => new PortfolioArchiveBuilder(fixture.Context, storage).BuildAsync([album.Id], null, default);
        await build.Should().ThrowAsync<ArchiveRequestException>().WithMessage("*снимка*Album*");
    }

    [Fact]
    public async Task Archive_RejectsEmptySelectionAndInactiveAlbums_WithoutExportingEverything()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var inactive = Album(Category("Inactive", false), "Album");
        fixture.Context.Add(inactive);
        await fixture.Context.SaveChangesAsync();
        var builder = new PortfolioArchiveBuilder(fixture.Context, new StubFileStorageService());
        foreach (var selection in new[] { Array.Empty<int>(), new[] { inactive.Id }, new[] { -1 }, new[] { 9999 } })
        {
            var build = () => builder.BuildAsync(selection, null, default);
            await build.Should().ThrowAsync<ArchiveRequestException>();
        }
    }

    [Fact]
    public async Task BulkMove_IsAtomic_PreservesPhotosAndAccess_AndAppendsOrder()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var old = Category("Old", false);
        var target = Category("New");
        var one = Album(old, "One", "/uploads/a.jpg");
        one.IsPublished = false;
        var two = Album(old, "Two");
        var existing = Album(target, "Existing");
        existing.DisplayOrder = 20;
        fixture.Context.AddRange(one, two, existing);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = new PortfolioAlbumBulkService(fixture.Context, audit);
        var invalid = await service.ExecuteAsync([one.Id, 9999], target.Id, Admin, default);
        invalid.StatusCode.Should().Be(400);
        fixture.Context.ChangeTracker.Clear();
        (await fixture.Context.PortfolioAlbums.FindAsync(one.Id))!.PortfolioCategoryId.Should().Be(old.Id);
        audit.Entries.Should().BeEmpty();
        var result = await service.ExecuteAsync([one.Id, two.Id, one.Id], target.Id, Admin, default);
        result.StatusCode.Should().Be(200);
        fixture.Context.ChangeTracker.Clear();
        var moved = await fixture.Context.PortfolioAlbums.Include(a => a.Images).Where(a => a.Id == one.Id || a.Id == two.Id).ToListAsync();
        moved.Should().OnlyContain(a => a.PortfolioCategoryId == target.Id && a.AllowClientAccess);
        moved.Select(a => a.DisplayOrder).Should().BeEquivalentTo(new[] { 21, 22 });
        moved.Single(a => a.Id == one.Id).Images.Should().ContainSingle();
        moved.Single(a => a.Id == one.Id).IsPublished.Should().BeFalse();
        audit.Entries.Should().ContainSingle(e => e.Action == "BulkMovePortfolioAlbums");
    }

    [Fact]
    public async Task BulkDelete_SoftDeletesOnlySelectedAlbumsAndPhotos_AndRevokesAccess()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var category = Category("Cat");
        var selected = Album(category, "Selected", "/uploads/a.jpg");
        selected.Images.First().IsCover = true;
        var keep = Album(category, "Keep", "/uploads/b.jpg");
        fixture.Context.AddRange(selected, keep);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var result = await new PortfolioAlbumBulkService(fixture.Context, audit).ExecuteAsync([selected.Id], null, Admin, default);
        result.StatusCode.Should().Be(200);
        fixture.Context.ChangeTracker.Clear();
        (await fixture.Context.PortfolioAlbums.ToListAsync()).Should().ContainSingle(a => a.Id == keep.Id);
        var deleted = await fixture.Context.PortfolioAlbums.IgnoreQueryFilters().Include(a => a.Images).SingleAsync(a => a.Id == selected.Id);
        deleted.IsDeleted.Should().BeTrue();
        deleted.AllowClientAccess.Should().BeFalse();
        deleted.IsPublished.Should().BeFalse();
        deleted.Images.Should().OnlyContain(p => p.IsDeleted && !p.IsPublished && !p.IsCover && p.DeletedAtUtc != null);
        audit.Entries.Should().ContainSingle(e => e.Action == "BulkSoftDeletePortfolioAlbums");
    }

    [Fact]
    public async Task BulkActions_RejectInvalidSelectionsAndCategories_WithoutAnyChanges()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var one = Album(Category("Active"), "One");
        var inactive = Category("Inactive", false);
        var user = Album(one.PortfolioCategory!, "User");
        user.IsUserUploaded = true;
        fixture.Context.AddRange(one, inactive, user);
        await fixture.Context.SaveChangesAsync();
        var audit = new RecordingAuditLogService();
        var service = new PortfolioAlbumBulkService(fixture.Context, audit);
        foreach (var selection in new int[]?[] { null, [], [-1], [one.Id, 9999], [one.Id, user.Id] })
            (await service.ExecuteAsync(selection, null, Admin, default)).StatusCode.Should().Be(400);
        foreach (var categoryId in new[] { inactive.Id, 9999, 0 })
            (await service.ExecuteAsync([one.Id], categoryId, Admin, default)).StatusCode.Should().Be(400);
        audit.Entries.Should().BeEmpty();
        fixture.Context.ChangeTracker.Clear();
        (await fixture.Context.PortfolioAlbums.ToListAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task ArchiveJob_IsOwnerScoped_RetrySafe_AndDownloadSurvivesCleanup()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var album = Album(Category("Category"), "Album", "/uploads/a.jpg");
        fixture.Context.Add(album);
        await fixture.Context.SaveChangesAsync();
        var storage = new StubFileStorageService();
        storage.Files["/uploads/a.jpg"] = [1, 2, 3];
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(fixture.Connection));
        services.AddSingleton<IFileStorageService>(storage);
        services.AddScoped<PortfolioArchiveBuilder>();
        await using var provider = services.BuildServiceProvider();
        using var jobs = new PortfolioArchiveJobs(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<PortfolioArchiveJobs>.Instance);
        var job = jobs.Enqueue("admin", [album.Id]);
        jobs.Enqueue("admin", [album.Id, album.Id]).Id.Should().Be(job.Id);
        jobs.Get(job.Id, "other-admin").Should().BeNull();
        jobs.Open(job.Id, "other-admin").Should().BeNull();
        await jobs.StartAsync(default);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (jobs.Get(job.Id, "admin")?.Status is not ("ready" or "failed")) await Task.Delay(20, timeout.Token);
            jobs.Get(job.Id, "admin")!.Status.Should().Be("ready");
            (await jobs.RemoveAsync(job.Id, "other-admin")).Should().BeFalse();
            var download = jobs.Open(job.Id, "admin")!;
            await using var stream = download.Stream;
            (await jobs.RemoveAsync(job.Id, "admin")).Should().BeTrue();
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            zip.Entries.Where(e => e.Name.Length > 0).Should().ContainSingle();
            jobs.Get(job.Id, "admin").Should().BeNull();
        }
        finally { await jobs.StopAsync(default); }
    }

    [Fact]
    public void ArchiveRoutes_AreUnambiguous_AndBulkAndJobEndpointsRequireAdmin()
    {
        var controllers = new[] { typeof(AdminClientGalleriesController), typeof(AdminClientGalleriesDownloadController) };
        controllers.SelectMany(c => c.GetMethods()).Count(m => m.GetCustomAttribute<HttpGetAttribute>()?.Template == "download-all")
            .Should().Be(1);
        foreach (var controller in new[] { typeof(AdminPortfolioAlbumBulkController), typeof(AdminPortfolioArchiveJobsController) })
            controller.GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("Admin");
    }
}
