using System.Data;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Api.Services;

public sealed record AdminCommandRequest(string? Command, int? ExpectedCount = null, bool Confirm = false);

public sealed record AlbumPhotoTextCommand(int? AlbumId)
{
    public const string Name = "albums.photos.clear-text";

    // Only documented, allow-listed commands are accepted. Never execute arbitrary input.
    public static bool TryParse(string? input, out AlbumPhotoTextCommand? command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(input) || input.Length > 256)
            return false;

        var args = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (args.Length < 2 || !args[0].Equals(Name, StringComparison.OrdinalIgnoreCase))
            return false;

        if (args.Length == 2 && args[1].Equals("--all", StringComparison.OrdinalIgnoreCase))
        {
            command = new AlbumPhotoTextCommand(null);
            return true;
        }

        if (args.Length == 3 &&
            args[1].Equals("--album", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(args[2], out var albumId) && albumId > 0)
        {
            command = new AlbumPhotoTextCommand(albumId);
            return true;
        }

        return false;
    }
}

public sealed class AdminCommandService(AppDbContext db, IAuditLogService audit)
{
    public async Task<ControllerServiceResult> PreviewAsync(
        AdminCommandRequest request, CancellationToken cancellationToken)
    {
        if (!AlbumPhotoTextCommand.TryParse(request.Command, out var command))
            return InvalidCommand();

        if (!await AlbumExistsAsync(command!, cancellationToken))
            return ControllerServiceResult.NotFound(new { message = "Албумът не съществува." });

        var query = AffectedPhotos(command!);
        var count = await query.CountAsync(cancellationToken);
        var albumCount = await query.Select(x => x.PortfolioAlbumId).Distinct().CountAsync(cancellationToken);

        return ControllerServiceResult.Ok(new
        {
            command = request.Command!.Trim(),
            count,
            albumCount,
            scope = command!.AlbumId is null ? "all" : "album",
            albumId = command.AlbumId,
            message = "Ще бъдат изчистени Name, AltText и Caption. Оригиналните снимки няма да бъдат променяни."
        });
    }

    public async Task<ControllerServiceResult> ExecuteAsync(
        AdminCommandRequest request, AdminRequestContext admin, CancellationToken cancellationToken)
    {
        if (!AlbumPhotoTextCommand.TryParse(request.Command, out var command))
            return InvalidCommand();

        if (!request.Confirm || request.ExpectedCount is null || request.ExpectedCount < 0)
            return ControllerServiceResult.BadRequest(new
            {
                message = "Първо направи предварителна проверка и потвърди операцията."
            });

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        if (!await AlbumExistsAsync(command!, cancellationToken))
            return ControllerServiceResult.NotFound(new { message = "Албумът не съществува." });

        var query = AffectedPhotos(command!);
        var count = await query.CountAsync(cancellationToken);
        if (count != request.ExpectedCount)
            return new ControllerServiceResult(409, new
            {
                message = "Данните са променени след проверката. Провери командата отново."
            });

        var albumCount = await query.Select(x => x.PortfolioAlbumId).Distinct().CountAsync(cancellationToken);
        var affected = count == 0 ? 0 : await query.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(x => x.Name, (string?)null)
                .SetProperty(x => x.AltText, (string?)null)
                .SetProperty(x => x.Caption, (string?)null),
            cancellationToken);

        await audit.LogAsync(
            admin.UserId, admin.Email,
            "AdminCommandClearAlbumPhotoText",
            "PortfolioImage",
            command!.AlbumId?.ToString(),
            new { Command = request.Command, PhotoCount = count, AlbumCount = albumCount },
            new { ClearedPhotoCount = affected, AlbumCount = albumCount },
            admin.RemoteIpAddress, admin.UserAgent, admin.TraceId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return ControllerServiceResult.Ok(new
        {
            command = request.Command!.Trim(),
            count = affected,
            albumCount,
            message = $"Изчистен е текстът на {affected} снимки в {albumCount} албума."
        });
    }

    private Task<bool> AlbumExistsAsync(AlbumPhotoTextCommand command, CancellationToken cancellationToken) =>
        command.AlbumId is null
            ? Task.FromResult(true)
            : db.PortfolioAlbums.AnyAsync(x => x.Id == command.AlbumId.Value, cancellationToken);

    private IQueryable<PortfolioImage> AffectedPhotos(AlbumPhotoTextCommand command)
    {
        // Exclude videos; only metadata on existing photos in non-deleted albums is changed.
        var query = db.PortfolioImages.Where(x =>
            x.PortfolioAlbum != null &&
            !x.PortfolioAlbum.IsDeleted &&
            !EF.Functions.ILike(x.ImageUrl, "%.mp4%") &&
            !EF.Functions.ILike(x.ImageUrl, "%.mov%") &&
            !EF.Functions.ILike(x.ImageUrl, "%.webm%") &&
            !EF.Functions.ILike(x.ImageUrl, "%.m4v%") &&
            (x.Name != null && x.Name != "" ||
             x.AltText != null && x.AltText != "" ||
             x.Caption != null && x.Caption != ""));

        return command.AlbumId.HasValue
            ? query.Where(x => x.PortfolioAlbumId == command.AlbumId.Value)
            : query;
    }

    private static ControllerServiceResult InvalidCommand() =>
        ControllerServiceResult.BadRequest(new
        {
            message = "Непозната команда. Използвай albums.photos.clear-text --all или albums.photos.clear-text --album 123."
        });
}
