using System.Security.Claims;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace DGVisionStudio.Tests.TestSupport;

internal sealed class GallerySqliteFixture : IAsyncDisposable
{
    private GallerySqliteFixture(SqliteConnection connection, AppDbContext context)
    {
        Connection = connection;
        Context = context;
    }

    public SqliteConnection Connection { get; }
    public AppDbContext Context { get; }

    public static async Task<GallerySqliteFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new GallerySqliteFixture(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await Connection.DisposeAsync();
    }
}

internal static class GalleryTestFiles
{
    public static IFormFile Create(string fileName, string contentType, byte[]? content = null)
    {
        content ??= [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}

internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "DGVisionStudio.Tests";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Test";
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class StubFileStorageService : IFileStorageService
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> DeletedPaths { get; } = [];
    public Exception? OpenReadException { get; set; }
    public Exception? DeleteException { get; set; }
    public string SavedPath { get; set; } = "/uploads/test/saved.png";

    public Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderPath, CancellationToken cancellationToken = default) =>
        SaveAsync(fileStream, SavedPath, cancellationToken);

    public Task<string> SaveImageAsync(Stream fileStream, string fileName, string folderPath, int maxWidth = 2400, int quality = 82, CancellationToken cancellationToken = default) =>
        SaveAsync(fileStream, SavedPath, cancellationToken);

    private async Task<string> SaveAsync(Stream stream, string path, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        Files[path] = memory.ToArray();
        return path;
    }

    public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (DeleteException is not null)
            throw DeleteException;
        DeletedPaths.Add(relativePath);
        Files.Remove(relativePath);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (OpenReadException is not null)
            throw OpenReadException;
        return Task.FromResult<Stream?>(Files.TryGetValue(relativePath, out var bytes)
            ? new MemoryStream(bytes, writable: false)
            : null);
    }

    public Task<bool> FileExistsAsync(string relativePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(Files.ContainsKey(relativePath));
}

internal sealed class RecordingAuditLogService : IAuditLogService
{
    public List<(string Action, string EntityType, string? EntityId)> Entries { get; } = [];

    public Task LogAsync(
        string adminUserId,
        string adminEmail,
        string action,
        string entityType,
        string? entityId,
        object? oldValue,
        object? newValue,
        string? ipAddress,
        string? userAgent,
        string? traceId,
        CancellationToken cancellationToken = default)
    {
        Entries.Add((action, entityType, entityId));
        return Task.CompletedTask;
    }
}

internal sealed class StubClientGalleryService : IClientGalleryService
{
    public Func<string, Task<List<MyClientGalleryDto>>> GetMyGalleries { get; set; } = _ => Task.FromResult<List<MyClientGalleryDto>>([]);
    public Func<int, string, Task<ClientGalleryDetailsDto?>> GetGalleryDetails { get; set; } = (_, _) => Task.FromResult<ClientGalleryDetailsDto?>(null);
    public Func<Task<List<MyClientGalleryDto>>> GetAllGalleries { get; set; } = () => Task.FromResult<List<MyClientGalleryDto>>([]);
    public Func<int, Task<ClientGalleryDetailsDto?>> GetGalleryById { get; set; } = _ => Task.FromResult<ClientGalleryDetailsDto?>(null);
    public Func<AdminCreateClientGalleryRequest, Task<int>> CreateGallery { get; set; } = _ => Task.FromResult(1);
    public Func<int, AdminUpdateClientGalleryRequest, Task<bool>> UpdateGallery { get; set; } = (_, _) => Task.FromResult(false);
    public Func<int, Task<bool>> DeleteGallery { get; set; } = _ => Task.FromResult(false);
    public Func<string, CreateUserClientGalleryRequest, Task<int?>> CreateUserGallery { get; set; } = (_, _) => Task.FromResult<int?>(null);
    public Func<int, string, IFormFile, Task<ClientPhotoDto?>> UploadUserPhoto { get; set; } = (_, _, _) => Task.FromResult<ClientPhotoDto?>(null);
    public Func<int, string, Task<bool>> DeleteUserGallery { get; set; } = (_, _) => Task.FromResult(false);
    public Func<int, string, bool, Task<bool>> UserCanAccess { get; set; } = (_, _, _) => Task.FromResult(false);
    public Func<int, int, string, bool, Task<(Stream Stream, string ContentType, string FileName)?>> OpenDownload { get; set; } = (_, _, _, _) => Task.FromResult<(Stream, string, string)?>(null);
    public Func<int, Task<List<GalleryUserAccessDto>>> GetAccesses { get; set; } = _ => Task.FromResult<List<GalleryUserAccessDto>>([]);
    public Func<int, GrantGalleryAccessRequest, Task<bool>> GrantAccess { get; set; } = (_, _) => Task.FromResult(false);
    public Func<int, string, UpdateGalleryAccessRequest, Task<bool>> UpdateAccess { get; set; } = (_, _, _) => Task.FromResult(false);
    public Func<int, string, Task<bool>> RemoveAccess { get; set; } = (_, _) => Task.FromResult(false);
    public Func<int, IFormFile, Task<ClientPhotoDto?>> UploadPhoto { get; set; } = (_, _) => Task.FromResult<ClientPhotoDto?>(null);
    public Func<int, int, UpdateClientPhotoRequest, Task<ClientPhotoDto?>> UpdatePhoto { get; set; } = (_, _, _) => Task.FromResult<ClientPhotoDto?>(null);
    public Func<int, int, Task<bool>> DeletePhoto { get; set; } = (_, _) => Task.FromResult(false);
    public Func<int, string, Task<bool>> SetCover { get; set; } = (_, _) => Task.FromResult(false);
    public Func<int, List<int>, Task<bool>> Reorder { get; set; } = (_, _) => Task.FromResult(false);
    public Func<CancellationToken, Task<int>> MarkExpired { get; set; } = _ => Task.FromResult(0);
    public Func<CancellationToken, Task<int>> DeleteExpired { get; set; } = _ => Task.FromResult(0);

    public Task<List<MyClientGalleryDto>> GetMyGalleriesAsync(string userId) => GetMyGalleries(userId);
    public Task<ClientGalleryDetailsDto?> GetGalleryDetailsAsync(int galleryId, string userId) => GetGalleryDetails(galleryId, userId);
    public Task<List<MyClientGalleryDto>> GetAllGalleriesAsync() => GetAllGalleries();
    public Task<ClientGalleryDetailsDto?> GetGalleryByIdAsync(int galleryId) => GetGalleryById(galleryId);
    public Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request) => CreateGallery(request);
    public Task<bool> UpdateGalleryAsync(int galleryId, AdminUpdateClientGalleryRequest request) => UpdateGallery(galleryId, request);
    public Task<bool> DeleteGalleryAsync(int galleryId) => DeleteGallery(galleryId);
    public Task<int?> CreateUserGalleryAsync(string userId, CreateUserClientGalleryRequest request) => CreateUserGallery(userId, request);
    public Task<ClientPhotoDto?> UploadUserGalleryPhotoAsync(int galleryId, string userId, IFormFile file) => UploadUserPhoto(galleryId, userId, file);
    public Task<bool> DeleteUserGalleryAsync(int galleryId, string userId) => DeleteUserGallery(galleryId, userId);
    public Task<bool> UserCanAccessGalleryAsync(int galleryId, string userId, bool requireDownload) => UserCanAccess(galleryId, userId, requireDownload);
    public Task<(Stream Stream, string ContentType, string FileName)?> OpenPhotoDownloadAsync(int galleryId, int photoId, string userId, bool isAdmin) => OpenDownload(galleryId, photoId, userId, isAdmin);
    public Task<List<GalleryUserAccessDto>> GetGalleryAccessesAsync(int galleryId) => GetAccesses(galleryId);
    public Task<bool> GrantAccessAsync(int galleryId, GrantGalleryAccessRequest request) => GrantAccess(galleryId, request);
    public Task<bool> UpdateAccessAsync(int galleryId, string userId, UpdateGalleryAccessRequest request) => UpdateAccess(galleryId, userId, request);
    public Task<bool> RemoveAccessAsync(int galleryId, string userId) => RemoveAccess(galleryId, userId);
    public Task<ClientPhotoDto?> UploadPhotoAsync(int galleryId, IFormFile file) => UploadPhoto(galleryId, file);
    public Task<ClientPhotoDto?> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request) => UpdatePhoto(galleryId, photoId, request);
    public Task<bool> DeletePhotoAsync(int galleryId, int photoId) => DeletePhoto(galleryId, photoId);
    public Task<bool> SetCoverImageAsync(int galleryId, string coverImageUrl) => SetCover(galleryId, coverImageUrl);
    public Task<bool> ReorderPhotosAsync(int galleryId, List<int> orderedPhotoIds) => Reorder(galleryId, orderedPhotoIds);
    public Task<int> MarkExpiredUserGalleriesAsync(CancellationToken cancellationToken = default) => MarkExpired(cancellationToken);
    public Task<int> DeleteExpiredUserGalleriesAsync(CancellationToken cancellationToken = default) => DeleteExpired(cancellationToken);
}

internal sealed class StubClientGalleryAdminService : IClientGalleryAdminService
{
    public List<MyClientGalleryDto> Galleries { get; set; } = [];
    public Task<List<MyClientGalleryDto>> GetAllGalleriesAsync() => Task.FromResult(Galleries);
    public Task<ClientGalleryDetailsDto?> GetGalleryByIdAsync(int galleryId) => Task.FromResult<ClientGalleryDetailsDto?>(null);
    public Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request) => Task.FromResult(1);
    public Task<bool> UpdateGalleryAsync(int galleryId, AdminUpdateClientGalleryRequest request) => Task.FromResult(false);
    public Task<bool> DeleteGalleryAsync(int galleryId) => Task.FromResult(false);
}

internal sealed class StubClientGalleryUserService : IClientGalleryUserService
{
    public Task<List<MyClientGalleryDto>> GetMyGalleriesAsync(string userId) => Task.FromResult<List<MyClientGalleryDto>>([]);
    public Task<ClientGalleryDetailsDto?> GetGalleryDetailsAsync(int galleryId, string userId) => Task.FromResult<ClientGalleryDetailsDto?>(null);
    public Task<int?> CreateUserGalleryAsync(string userId, CreateUserClientGalleryRequest request) => Task.FromResult<int?>(null);
    public Task<ClientPhotoDto?> UploadUserGalleryPhotoAsync(int galleryId, string userId, IFormFile file) => Task.FromResult<ClientPhotoDto?>(null);
    public Task<bool> DeleteUserGalleryAsync(int galleryId, string userId) => Task.FromResult(false);
    public Task<bool> UserCanAccessGalleryAsync(int galleryId, string userId, bool requireDownload) => Task.FromResult(false);
}

internal sealed class StubClientGalleryAccessService : IClientGalleryAccessService
{
    public Task<List<GalleryUserAccessDto>> GetGalleryAccessesAsync(int galleryId) => Task.FromResult<List<GalleryUserAccessDto>>([]);
    public Task<bool> GrantAccessAsync(int galleryId, GrantGalleryAccessRequest request) => Task.FromResult(false);
    public Task<bool> UpdateAccessAsync(int galleryId, string userId, UpdateGalleryAccessRequest request) => Task.FromResult(false);
    public Task<bool> RemoveAccessAsync(int galleryId, string userId) => Task.FromResult(false);
    public Task SyncUserAccessesAsync(int galleryId, List<GalleryUserAccessDto>? requestedAccesses) => Task.CompletedTask;
}

internal sealed class StubClientGalleryPhotoService : IClientGalleryPhotoService
{
    public Task<(Stream Stream, string ContentType, string FileName)?> OpenPhotoDownloadAsync(int galleryId, int photoId, string userId, bool isAdmin) => Task.FromResult<(Stream, string, string)?>(null);
    public Task<ClientPhotoDto?> UploadPhotoAsync(int galleryId, IFormFile file) => Task.FromResult<ClientPhotoDto?>(null);
    public Task<ClientPhotoDto?> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request) => Task.FromResult<ClientPhotoDto?>(null);
    public Task<bool> DeletePhotoAsync(int galleryId, int photoId) => Task.FromResult(false);
    public Task<bool> SetCoverImageAsync(int galleryId, string coverImageUrl) => Task.FromResult(false);
    public Task<bool> ReorderPhotosAsync(int galleryId, List<int> orderedPhotoIds) => Task.FromResult(false);
}

internal sealed class StubClientGalleryExpiryService : IClientGalleryExpiryService
{
    public Task<int> MarkExpiredUserGalleriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task<int> DeleteExpiredUserGalleriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}
