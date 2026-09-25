using System.IO.Compression;
using System.Security.Cryptography;
using DGVisionStudio.Api.Services;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Tests.TestSupport;
using FluentAssertions;

namespace DGVisionStudio.Tests.Portfolio;

public sealed class PortfolioArchiveRefactorTests
{
    [Fact]
    public async Task SelectionService_FiltersInactiveCategoriesAndSelectedAlbums()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var active = Category("Active", true);
        var inactive = Category("Inactive", false);
        var selected = Album(active, "Selected");
        var other = Album(active, "Other");
        var hidden = Album(inactive, "Hidden");
        fixture.Context.AddRange(selected, other, hidden);
        await fixture.Context.SaveChangesAsync();

        var selection = await new PortfolioArchiveSelectionService(
            fixture.Context).LoadAsync([selected.Id], default);

        selection.Categories.Should().ContainSingle(
            category => category.Id == active.Id);
        selection.Albums.Should().ContainSingle(
            album => album.Id == selected.Id);
    }

    [Fact]
    public async Task SelectionService_RejectsInvalidOrMissingSelections()
    {
        await using var fixture = await GallerySqliteFixture.CreateAsync();
        var builder = new PortfolioArchiveSelectionService(
            fixture.Context);

        var empty = () => builder.LoadAsync([], default);
        var invalid = () => builder.LoadAsync([-1], default);
        var missing = () => builder.LoadAsync([999999], default);

        await empty.Should()
            .ThrowAsync<ArchiveRequestException>();
        await invalid.Should()
            .ThrowAsync<ArchiveRequestException>();
        await missing.Should()
            .ThrowAsync<ArchiveRequestException>();
    }

    [Fact]
    public void NameService_SanitizesReservedDuplicateAndInvalidExtensionNames()
    {
        var service = new PortfolioArchiveNameService();
        var albums = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        service.AlbumSegment("CON", 7, albums)
            .Should().Be("_CON");
        service.AlbumSegment("CON", 8, albums)
            .Should().Be("_CON-8");
        service.PhotoFileName(
                "name.exe",
                "/uploads/photo.very-long-extension",
                9,
                files)
            .Should().Be("name.exe.jpg");
    }

    [Fact]
    public async Task PhotoWriter_CopiesStoredBytesAndProducesMatchingDigest()
    {
        var storage = new StubFileStorageService();
        storage.Files["/uploads/photo.jpg"] = [4, 5, 6, 7];
        var service = new PortfolioArchivePhotoWriter(storage);
        await using var target = new MemoryStream();

        var digest = await service.CopyAsync(
            "/uploads/photo.jpg",
            target,
            default);

        target.ToArray().Should().Equal(4, 5, 6, 7);
        digest.Length.Should().Be(4);
        digest.Hash.Should().Equal(
            SHA256.HashData([4, 5, 6, 7]));
    }

    [Fact]
    public async Task PhotoWriter_RejectsMissingNonStaticAndEmptySources()
    {
        var storage = new StubFileStorageService();
        storage.Files["/uploads/empty.jpg"] = [];
        var service = new PortfolioArchivePhotoWriter(storage);

        var missing = () => service.CopyAsync(
            "/uploads/missing.jpg",
            new MemoryStream(),
            default);
        var empty = () => service.CopyAsync(
            "/uploads/empty.jpg",
            new MemoryStream(),
            default);

        await missing.Should().ThrowAsync<IOException>()
            .WithMessage("Source not found.");
        await empty.Should().ThrowAsync<IOException>()
            .WithMessage("Empty source file.");
    }

    [Fact]
    public async Task Verifier_AcceptsMatchingArchiveAndRejectsChangedDigest()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"dg-archive-verifier-{Guid.NewGuid():N}.zip");
        try
        {
            var bytes = new byte[] { 1, 2, 3 };
            using (var zip = ZipFile.Open(
                       path,
                       ZipArchiveMode.Create))
            {
                zip.CreateEntry("root/");
                var entry = zip.CreateEntry("root/photo.jpg");
                await using var stream = entry.Open();
                await stream.WriteAsync(bytes);
            }

            var verifier = new PortfolioArchiveVerifier();
            var manifest =
                new Dictionary<string, ArchiveEntryDigest>
                {
                    ["root/photo.jpg"] = new(
                        bytes.Length,
                        SHA256.HashData(bytes))
                };

            await verifier.VerifyAsync(
                path,
                manifest,
                directoryCount: 1,
                default);

            manifest["root/photo.jpg"] = new(
                bytes.Length,
                SHA256.HashData([9, 9, 9]));

            var invalid = () => verifier.VerifyAsync(
                path,
                manifest,
                directoryCount: 1,
                default);

            await invalid.Should()
                .ThrowAsync<IOException>()
                .WithMessage("Archive verification failed.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PortfolioCategory Category(
        string name,
        bool active) =>
        new()
        {
            Key = Guid.NewGuid().ToString("N"),
            Name = name,
            NameEn = name,
            IsActive = active
        };

    private static PortfolioAlbum Album(
        PortfolioCategory category,
        string title) =>
        new()
        {
            PortfolioCategory = category,
            Slug = Guid.NewGuid().ToString("N"),
            Title = title
        };
}
