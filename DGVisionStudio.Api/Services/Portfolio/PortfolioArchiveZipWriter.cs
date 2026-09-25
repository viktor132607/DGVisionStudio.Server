using System.IO.Compression;
using System.Text;

namespace DGVisionStudio.Api.Services;

public sealed record PortfolioArchiveWriteResult(
    IReadOnlyDictionary<string, ArchiveEntryDigest> Manifest,
    int DirectoryCount,
    int CompletedFiles);

public sealed class PortfolioArchiveZipWriter(
    PortfolioArchiveNameService names,
    PortfolioArchivePhotoWriter photos)
{
    public async Task<PortfolioArchiveWriteResult> WriteAsync(
        string path,
        string root,
        PortfolioArchiveSelection selection,
        Action<int>? fileCompleted,
        CancellationToken token)
    {
        var manifest =
            new Dictionary<string, ArchiveEntryDigest>(
                StringComparer.Ordinal);
        var directoryCount = 0;
        var completed = 0;

        await using var file = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            81920,
            FileOptions.Asynchronous |
            FileOptions.SequentialScan);

        using var zip = new ZipArchive(
            file,
            ZipArchiveMode.Create,
            leaveOpen: true,
            Encoding.UTF8);

        void DirectoryEntry(string name)
        {
            zip.CreateEntry(name + "/");
            directoryCount++;
        }

        DirectoryEntry(root);

        var categoryNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var category in selection.Categories)
        {
            token.ThrowIfCancellationRequested();

            var categoryPath =
                root + "/" + names.CategorySegment(
                    category.Name,
                    category.Id,
                    categoryNames);

            DirectoryEntry(categoryPath);

            var albumNames =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var album in selection.Albums.Where(
                         album =>
                             album.PortfolioCategoryId ==
                             category.Id))
            {
                var albumPath =
                    categoryPath + "/" + names.AlbumSegment(
                        album.Title,
                        album.Id,
                        albumNames);

                DirectoryEntry(albumPath);

                var fileNames =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);

                foreach (var photo in album.Images
                             .OrderBy(photo => photo.DisplayOrder)
                             .ThenBy(photo => photo.Id))
                {
                    token.ThrowIfCancellationRequested();

                    var entryPath =
                        albumPath + "/" + names.PhotoFileName(
                            photo.Name,
                            photo.ImageUrl,
                            photo.Id,
                            fileNames);

                    var entry = zip.CreateEntry(
                        entryPath,
                        CompressionLevel.NoCompression);

                    try
                    {
                        await using var target = entry.Open();
                        manifest.Add(
                            entryPath,
                            await photos.CopyAsync(
                                photo.ImageUrl ?? string.Empty,
                                target,
                                token));
                    }
                    catch (Exception ex) when (
                        ex is not OperationCanceledException)
                    {
                        throw new ArchiveRequestException(
                            $"Архивът не е създаден: снимка №{photo.Id} в албум „{album.Title}“ не може да бъде прочетена. Провери файла и опитай отново.");
                    }

                    fileCompleted?.Invoke(++completed);
                }
            }
        }

        return new PortfolioArchiveWriteResult(
            manifest,
            directoryCount,
            completed);
    }
}
