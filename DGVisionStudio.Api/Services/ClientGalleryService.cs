using System.Text.RegularExpressions;
using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Services;

public class ClientGalleryService : IClientGalleryService
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFileStorageService _fileStorageService;

    public ClientGalleryService(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IFileStorageService fileStorageService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _fileStorageService = fileStorageService;
    }

    public async Task<List<MyClientGalleryDto>> GetMyGalleriesAsync(string userId)
    {
        var now = DateTime.UtcNow;

        var accesses = await _dbContext.UserAlbumAccesses
            .AsNoTracking()
            .Include(x => x.PortfolioAlbum)
                .ThenInclude(x => x.PortfolioCategory)
            .Where(x => x.UserId == userId && x.PortfolioAlbum.AllowClientAccess)
            .OrderByDescending(x => x.PortfolioAlbum.CreatedAtUtc)
            .ThenByDescending(x => x.PortfolioAlbumId)
            .ToListAsync();

        return accesses.Select(x => new MyClientGalleryDto
        {
            Id = x.PortfolioAlbumId,
            Title = x.PortfolioAlbum.Title,
            TitleEn = x.PortfolioAlbum.TitleEn,
            Description = x.PortfolioAlbum.Description,
            CoverImageUrl = x.PortfolioAlbum.CoverImageUrl,
            IsActive = x.PortfolioAlbum.IsActive,
            IsPublic = x.PortfolioAlbum.PortfolioCategory != null &&
                       x.PortfolioAlbum.PortfolioCategory.Key != "client-galleries",
            IsPublished = x.PortfolioAlbum.IsPublished,
            PortfolioCategoryId = x.PortfolioAlbum.PortfolioCategoryId,
            PortfolioCategoryName = x.PortfolioAlbum.PortfolioCategory?.Name,
            PortfolioCategoryNameEn = x.PortfolioAlbum.PortfolioCategory?.NameEn,
            PreviewEnabled = x.PreviewEnabled,
            DownloadEnabled = IsDownloadActive(x, now),
            DownloadExpiresAtUtc = x.DownloadExpiresAtUtc,
            RemainingDownloadDays = GetRemainingDays(x, now),
            IsExpired = IsExpired(x, now)
        }).ToList();
    }

    public async Task<ClientGalleryDetailsDto?> GetGalleryDetailsAsync(int galleryId, string userId)
    {
        var now = DateTime.UtcNow;

        var access = await _dbContext.UserAlbumAccesses
            .AsNoTrackingWithIdentityResolution()
            .Include(x => x.PortfolioAlbum)
                .ThenInclude(x => x.Images)
            .Include(x => x.PortfolioAlbum)
                .ThenInclude(x => x.UserAccesses)
                    .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.PortfolioAlbumId == galleryId &&
                x.UserId == userId &&
                x.PortfolioAlbum.AllowClientAccess);

        if (access == null)
            return null;

        var canDownload = IsDownloadActive(access, now);

        return new ClientGalleryDetailsDto
        {
            Id = access.PortfolioAlbumId,
            Title = access.PortfolioAlbum.Title,
            TitleEn = access.PortfolioAlbum.TitleEn,
            Description = access.PortfolioAlbum.Description,
            CoverImageUrl = access.PortfolioAlbum.CoverImageUrl,
            IsActive = access.PortfolioAlbum.IsActive,
            IsPublic = access.PortfolioAlbum.PortfolioCategoryId > 0,
            IsPublished = access.PortfolioAlbum.IsPublished,
            PortfolioCategoryId = access.PortfolioAlbum.PortfolioCategoryId,
            PreviewEnabled = access.PreviewEnabled,
            DownloadEnabled = canDownload,
            DownloadExpiresAtUtc = access.DownloadExpiresAtUtc,
            RemainingDownloadDays = GetRemainingDays(access, now),
            IsExpired = IsExpired(access, now),
            UserAccesses = access.PortfolioAlbum.UserAccesses
                .OrderBy(x => x.User.Email)
                .Select(x => new GalleryUserAccessDto
                {
                    UserId = x.UserId,
                    Email = x.User.Email ?? string.Empty,
                    PreviewEnabled = x.PreviewEnabled,
                    DownloadEnabled = x.DownloadEnabled,
                    DownloadExpiresAtUtc = x.DownloadExpiresAtUtc
                })
                .ToList(),
            Photos = access.PortfolioAlbum.Images
                .Where(x => x.IsPublished)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Id)
                .Select(x => MapPhotoDto(x, canDownload))
                .ToList()
        };
    }

    public async Task<List<MyClientGalleryDto>> GetAllGalleriesAsync()
    {
        var now = DateTime.UtcNow;

        var albums = await _dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.PortfolioCategory)
            .Include(x => x.UserAccesses)
            .Where(x => x.AllowClientAccess || x.IsActive)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        return albums.Select(x =>
        {
            var firstAccess = x.UserAccesses
                .OrderByDescending(a => a.DownloadEnabled)
                .ThenByDescending(a => a.DownloadExpiresAtUtc)
                .FirstOrDefault();

            return new MyClientGalleryDto
            {
                Id = x.Id,
                Title = x.Title,
                TitleEn = x.TitleEn,
                Description = x.Description,
                CoverImageUrl = x.CoverImageUrl,
                IsActive = x.IsActive,
                IsPublic = x.PortfolioCategory != null &&
                           x.PortfolioCategory.Key != "client-galleries",
                IsPublished = x.IsPublished,
                PortfolioCategoryId = x.PortfolioCategoryId,
                PortfolioCategoryName = x.PortfolioCategory?.Name,
                PortfolioCategoryNameEn = x.PortfolioCategory?.NameEn,
                PreviewEnabled = x.UserAccesses.Any(a => a.PreviewEnabled),
                DownloadEnabled = firstAccess != null && IsDownloadActive(firstAccess, now),
                DownloadExpiresAtUtc = firstAccess?.DownloadExpiresAtUtc,
                RemainingDownloadDays = firstAccess != null ? GetRemainingDays(firstAccess, now) : null,
                IsExpired = firstAccess != null && IsExpired(firstAccess, now)
            };
        }).ToList();
    }

    public async Task<ClientGalleryDetailsDto?> GetGalleryByIdAsync(int galleryId)
    {
        var album = await _dbContext.PortfolioAlbums
            .AsNoTracking()
            .Include(x => x.Images)
            .Include(x => x.UserAccesses)
                .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == galleryId && (x.AllowClientAccess || x.IsActive));

        if (album == null)
            return null;

        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Select(x => new AdminGalleryUserOptionDto
            {
                Id = x.Id,
                Email = x.Email ?? string.Empty
            })
            .ToListAsync();

        var previewEnabled = album.UserAccesses.Any(x => x.PreviewEnabled);
        var now = DateTime.UtcNow;
        var firstDownloadAccess = album.UserAccesses
            .OrderByDescending(x => x.DownloadEnabled)
            .ThenByDescending(x => x.DownloadExpiresAtUtc)
            .FirstOrDefault();

        return new ClientGalleryDetailsDto
        {
            Id = album.Id,
            Title = album.Title,
            TitleEn = album.TitleEn,
            Description = album.Description,
            CoverImageUrl = album.CoverImageUrl,
            IsActive = album.IsActive,
            IsPublic = album.PortfolioCategoryId > 0,
            IsPublished = album.IsPublished,
            PortfolioCategoryId = album.PortfolioCategoryId,
            PreviewEnabled = previewEnabled,
            DownloadEnabled = firstDownloadAccess != null && IsDownloadActive(firstDownloadAccess, now),
            DownloadExpiresAtUtc = firstDownloadAccess?.DownloadExpiresAtUtc,
            RemainingDownloadDays = firstDownloadAccess != null ? GetRemainingDays(firstDownloadAccess, now) : null,
            IsExpired = firstDownloadAccess != null && IsExpired(firstDownloadAccess, now),
            AvailableUsers = users,
            UserAccesses = album.UserAccesses
                .OrderBy(x => x.User.Email)
                .Select(x => new GalleryUserAccessDto
                {
                    UserId = x.UserId,
                    Email = x.User.Email ?? string.Empty,
                    PreviewEnabled = x.PreviewEnabled,
                    DownloadEnabled = x.DownloadEnabled,
                    DownloadExpiresAtUtc = x.DownloadExpiresAtUtc
                })
                .ToList(),
            Photos = album.Images
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Id)
                .Select(x => MapPhotoDto(x, true))
                .ToList()
        };
    }

    public async Task<int> CreateGalleryAsync(AdminCreateClientGalleryRequest request)
    {
        var categoryId = request.IsPublic
            ? request.PortfolioCategoryId ?? await EnsureClientAlbumsCategoryAsync()
            : await EnsureClientAlbumsCategoryAsync();

        var maxDisplayOrder = await _dbContext.PortfolioAlbums
            .Where(x => x.PortfolioCategoryId == categoryId)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync() ?? 0;

        var title = request.Title.Trim();
        var titleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? null : request.TitleEn.Trim();

        var album = new PortfolioAlbum
        {
            PortfolioCategoryId = categoryId,
            Slug = await BuildUniqueSlugAsync(titleEn ?? title),
            Title = title,
            TitleEn = titleEn,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim(),
            DisplayOrder = maxDisplayOrder + 1,
            IsPublished = request.IsPublic && request.IsPublished,
            IsActive = request.IsActive,
            AllowClientAccess = !request.IsPublic,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PortfolioAlbums.Add(album);
        await _dbContext.SaveChangesAsync();

        await SyncUserAccessesAsync(album.Id, request.UserAccesses);

        return album.Id;
    }

    public async Task<bool> UpdateGalleryAsync(int galleryId, AdminUpdateClientGalleryRequest request)
    {
        var album = await _dbContext.PortfolioAlbums
            .Include(x => x.UserAccesses)
            .FirstOrDefaultAsync(x => x.Id == galleryId);

        if (album == null)
            return false;

        var title = request.Title.Trim();
        var titleEn = string.IsNullOrWhiteSpace(request.TitleEn) ? null : request.TitleEn.Trim();

        if (!string.Equals(album.Title, title, StringComparison.Ordinal) ||
            !string.Equals(album.TitleEn, titleEn, StringComparison.Ordinal))
        {
            album.Slug = await BuildUniqueSlugAsync(titleEn ?? title, galleryId);
        }

        album.Title = title;
        album.TitleEn = titleEn;
        album.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        album.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
        album.IsActive = request.IsActive;
        album.AllowClientAccess = !request.IsPublic;
        album.IsPublished = request.IsPublic && request.IsPublished;
        album.PortfolioCategoryId = request.IsPublic
            ? request.PortfolioCategoryId ?? album.PortfolioCategoryId
            : await EnsureClientAlbumsCategoryAsync();

        await _dbContext.SaveChangesAsync();

        await SyncUserAccessesAsync(album.Id, request.UserAccesses);

        return true;
    }

    public async Task<bool> DeleteGalleryAsync(int galleryId)
    {
        var album = await _dbContext.PortfolioAlbums
            .Include(x => x.Images)
            .Include(x => x.UserAccesses)
            .FirstOrDefaultAsync(x => x.Id == galleryId);

        if (album == null)
            return false;

        foreach (var image in album.Images)
        {
            if (!string.IsNullOrWhiteSpace(image.ImageUrl))
                await _fileStorageService.DeleteFileAsync(image.ImageUrl);

            if (!string.IsNullOrWhiteSpace(image.ThumbnailUrl) &&
                !string.Equals(image.ThumbnailUrl, image.ImageUrl, StringComparison.OrdinalIgnoreCase))
            {
                await _fileStorageService.DeleteFileAsync(image.ThumbnailUrl);
            }
        }

        _dbContext.UserAlbumAccesses.RemoveRange(album.UserAccesses);
        _dbContext.PortfolioImages.RemoveRange(album.Images);
        _dbContext.PortfolioAlbums.Remove(album);

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<List<GalleryUserAccessDto>> GetGalleryAccessesAsync(int galleryId)
    {
        return await _dbContext.UserAlbumAccesses
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.PortfolioAlbumId == galleryId)
            .OrderBy(x => x.User.Email)
            .Select(x => new GalleryUserAccessDto
            {
                UserId = x.UserId,
                Email = x.User.Email ?? string.Empty,
                PreviewEnabled = x.PreviewEnabled,
                DownloadEnabled = x.DownloadEnabled,
                DownloadExpiresAtUtc = x.DownloadExpiresAtUtc
            })
            .ToListAsync();
    }

    public async Task<bool> GrantAccessAsync(int galleryId, GrantGalleryAccessRequest request)
    {
        var albumExists = await _dbContext.PortfolioAlbums
            .AnyAsync(x => x.Id == galleryId && x.AllowClientAccess);

        if (!albumExists)
            return false;

        var user = await _userManager.FindByEmailAsync(request.UserEmail.Trim());
        if (user == null)
            return false;

        var access = await _dbContext.UserAlbumAccesses
            .FirstOrDefaultAsync(x => x.PortfolioAlbumId == galleryId && x.UserId == user.Id);

        if (access == null)
        {
            access = new UserAlbumAccess
            {
                PortfolioAlbumId = galleryId,
                UserId = user.Id,
                PreviewEnabled = request.PreviewEnabled,
                DownloadEnabled = request.DownloadEnabled,
                DownloadExpiresAtUtc = request.DownloadExpiresAtUtc
            };

            _dbContext.UserAlbumAccesses.Add(access);
        }
        else
        {
            access.PreviewEnabled = request.PreviewEnabled;
            access.DownloadEnabled = request.DownloadEnabled;
            access.DownloadExpiresAtUtc = request.DownloadExpiresAtUtc;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAccessAsync(int galleryId, string userId, UpdateGalleryAccessRequest request)
    {
        var access = await _dbContext.UserAlbumAccesses
            .FirstOrDefaultAsync(x => x.PortfolioAlbumId == galleryId && x.UserId == userId);

        if (access == null)
            return false;

        access.PreviewEnabled = request.PreviewEnabled;
        access.DownloadEnabled = request.DownloadEnabled;
        access.DownloadExpiresAtUtc = request.DownloadExpiresAtUtc;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAccessAsync(int galleryId, string userId)
    {
        var access = await _dbContext.UserAlbumAccesses
            .FirstOrDefaultAsync(x => x.PortfolioAlbumId == galleryId && x.UserId == userId);

        if (access == null)
            return false;

        _dbContext.UserAlbumAccesses.Remove(access);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<ClientPhotoDto?> UploadPhotoAsync(int galleryId, IFormFile file)
    {
        var album = await _dbContext.PortfolioAlbums.FirstOrDefaultAsync(x => x.Id == galleryId);
        if (album == null)
            return null;

        await using var stream = file.OpenReadStream();
        var savedPath = await _fileStorageService.SaveFileAsync(
            stream,
            file.FileName,
            Path.Combine("uploads", "portfolio"),
            CancellationToken.None);

        var nextDisplayOrder = await _dbContext.PortfolioImages
            .Where(x => x.PortfolioAlbumId == galleryId)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync() ?? 0;

        var photo = new PortfolioImage
        {
            PortfolioAlbumId = galleryId,
            ImageUrl = savedPath,
            ThumbnailUrl = savedPath,
            AltText = Path.GetFileNameWithoutExtension(file.FileName),
            Caption = null,
            DisplayOrder = nextDisplayOrder + 1,
            IsCover = false,
            IsPublished = true
        };

        _dbContext.PortfolioImages.Add(photo);

        if (string.IsNullOrWhiteSpace(album.CoverImageUrl))
        {
            album.CoverImageUrl = savedPath;
            photo.IsCover = true;
        }

        await _dbContext.SaveChangesAsync();
        return MapPhotoDto(photo, true);
    }

    public async Task<ClientPhotoDto?> UpdatePhotoAsync(int galleryId, int photoId, UpdateClientPhotoRequest request)
    {
        var photo = await _dbContext.PortfolioImages
            .Include(x => x.PortfolioAlbum)
                .ThenInclude(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == photoId && x.PortfolioAlbumId == galleryId);

        if (photo == null)
            return null;

        photo.AltText = string.IsNullOrWhiteSpace(request.AltText) ? null : request.AltText.Trim();
        photo.Caption = string.IsNullOrWhiteSpace(request.Caption)
            ? (string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim())
            : request.Caption.Trim();
        photo.DisplayOrder = request.DisplayOrder ?? photo.DisplayOrder;

        if (request.IsPublished.HasValue)
        {
            photo.IsPublished = request.IsPublished.Value;
        }

        if (request.IsCover == true && photo.PortfolioAlbum != null)
        {
            foreach (var image in photo.PortfolioAlbum.Images)
            {
                image.IsCover = image.Id == photo.Id;
            }

            photo.PortfolioAlbum.CoverImageUrl = photo.ThumbnailUrl ?? photo.ImageUrl;
        }

        await _dbContext.SaveChangesAsync();
        return MapPhotoDto(photo, true);
    }

    public async Task<bool> DeletePhotoAsync(int galleryId, int photoId)
    {
        var album = await _dbContext.PortfolioAlbums
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == galleryId);

        if (album == null)
            return false;

        var photo = album.Images.FirstOrDefault(x => x.Id == photoId);
        if (photo == null)
            return false;

        if (!string.IsNullOrWhiteSpace(photo.ImageUrl))
            await _fileStorageService.DeleteFileAsync(photo.ImageUrl);

        if (!string.IsNullOrWhiteSpace(photo.ThumbnailUrl) &&
            !string.Equals(photo.ThumbnailUrl, photo.ImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            await _fileStorageService.DeleteFileAsync(photo.ThumbnailUrl);
        }

        _dbContext.PortfolioImages.Remove(photo);

        if (string.Equals(album.CoverImageUrl, photo.ThumbnailUrl, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(album.CoverImageUrl, photo.ImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = album.Images
                .Where(x => x.Id != photoId)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Id)
                .FirstOrDefault();

            album.CoverImageUrl = fallback?.ThumbnailUrl ?? fallback?.ImageUrl;

            foreach (var image in album.Images)
            {
                image.IsCover = fallback != null && image.Id == fallback.Id;
            }
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetCoverImageAsync(int galleryId, string coverImageUrl)
    {
        var album = await _dbContext.PortfolioAlbums
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == galleryId);

        if (album == null)
            return false;

        var matchingPhoto = album.Images.FirstOrDefault(x =>
            string.Equals(x.ThumbnailUrl, coverImageUrl, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.ImageUrl, coverImageUrl, StringComparison.OrdinalIgnoreCase));

        if (matchingPhoto == null)
            return false;

        foreach (var image in album.Images)
        {
            image.IsCover = image.Id == matchingPhoto.Id;
        }

        album.CoverImageUrl = matchingPhoto.ThumbnailUrl ?? matchingPhoto.ImageUrl;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReorderPhotosAsync(int galleryId, List<int> orderedPhotoIds)
    {
        var photos = await _dbContext.PortfolioImages
            .Where(x => x.PortfolioAlbumId == galleryId)
            .ToListAsync();

        if (photos.Count == 0)
            return false;

        var photoMap = photos.ToDictionary(x => x.Id);
        var order = 1;

        foreach (var photoId in orderedPhotoIds)
        {
            if (photoMap.TryGetValue(photoId, out var photo))
            {
                photo.DisplayOrder = order++;
            }
        }

        foreach (var remaining in photos
            .Where(x => !orderedPhotoIds.Contains(x.Id))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id))
        {
            remaining.DisplayOrder = order++;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    private async Task SyncUserAccessesAsync(int galleryId, List<GalleryUserAccessDto>? requestedAccesses)
    {
        var normalized = requestedAccesses ?? new List<GalleryUserAccessDto>();

        var existing = await _dbContext.UserAlbumAccesses
            .Where(x => x.PortfolioAlbumId == galleryId)
            .ToListAsync();

        var desiredByUserId = new Dictionary<string, GalleryUserAccessDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in normalized)
        {
            string? userId = null;

            if (!string.IsNullOrWhiteSpace(item.UserId))
            {
                userId = item.UserId.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(item.Email))
            {
                var user = await _userManager.FindByEmailAsync(item.Email.Trim());
                userId = user?.Id;
            }

            if (string.IsNullOrWhiteSpace(userId))
                continue;

            desiredByUserId[userId] = item;
        }

        foreach (var existingAccess in existing)
        {
            if (!desiredByUserId.ContainsKey(existingAccess.UserId))
            {
                _dbContext.UserAlbumAccesses.Remove(existingAccess);
            }
        }

        foreach (var pair in desiredByUserId)
        {
            var userId = pair.Key;
            var item = pair.Value;

            var access = existing.FirstOrDefault(x => x.UserId == userId);
            if (access == null)
            {
                access = new UserAlbumAccess
                {
                    PortfolioAlbumId = galleryId,
                    UserId = userId
                };

                _dbContext.UserAlbumAccesses.Add(access);
            }

            access.PreviewEnabled = item.PreviewEnabled;
            access.DownloadEnabled = item.DownloadEnabled;
            access.DownloadExpiresAtUtc = item.DownloadExpiresAtUtc;
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task<int> EnsureClientAlbumsCategoryAsync()
    {
        var existing = await _dbContext.PortfolioCategories
            .FirstOrDefaultAsync(x => x.Key == "client-galleries");

        if (existing != null)
            return existing.Id;

        var maxOrder = await _dbContext.PortfolioCategories
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync() ?? 0;

        var category = new PortfolioCategory
        {
            Key = "client-galleries",
            Name = "Клиентски албуми",
            NameEn = "Client Galleries",
            Description = "Albums created from the client gallery admin.",
            DisplayOrder = maxOrder + 1,
            IsActive = false
        };

        _dbContext.PortfolioCategories.Add(category);
        await _dbContext.SaveChangesAsync();
        return category.Id;
    }

    private async Task<string> BuildUniqueSlugAsync(string title, int? currentAlbumId = null)
    {
        var baseSlug = Slugify(title);
        var slug = baseSlug;
        var index = 2;

        while (await _dbContext.PortfolioAlbums.AnyAsync(x => x.Slug == slug && x.Id != currentAlbumId))
        {
            slug = $"{baseSlug}-{index}";
            index++;
        }

        return slug;
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Guid.NewGuid().ToString("N");

        var slug = value.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9а-я]+", "-");
        slug = Regex.Replace(slug, @"-+", "-").Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
    }

    private static ClientPhotoDto MapPhotoDto(PortfolioImage image, bool canDownload)
    {
        var previewUrl = string.IsNullOrWhiteSpace(image.ThumbnailUrl)
            ? image.ImageUrl
            : image.ThumbnailUrl!;

        return new ClientPhotoDto
        {
            Id = image.Id,
            PreviewUrl = previewUrl,
            OriginalUrl = canDownload ? image.ImageUrl : null,
            AltText = image.AltText,
            Caption = image.Caption,
            CanDownload = canDownload && !string.IsNullOrWhiteSpace(image.ImageUrl),
            DisplayOrder = image.DisplayOrder,
            Description = image.Caption,
            IsPublished = image.IsPublished,
            ShowInPublicGallery = false,
            VisibleToAllAuthorizedUsers = true,
            AllowedUserIds = new List<string>()
        };
    }

    private static bool IsDownloadActive(UserAlbumAccess access, DateTime now)
    {
        if (!access.DownloadEnabled)
            return false;

        if (!access.DownloadExpiresAtUtc.HasValue)
            return true;

        return access.DownloadExpiresAtUtc.Value >= now;
    }

    private static bool IsExpired(UserAlbumAccess access, DateTime now)
    {
        return access.DownloadEnabled &&
               access.DownloadExpiresAtUtc.HasValue &&
               access.DownloadExpiresAtUtc.Value < now;
    }

    private static int? GetRemainingDays(UserAlbumAccess access, DateTime now)
    {
        if (!access.DownloadEnabled || !access.DownloadExpiresAtUtc.HasValue)
            return null;

        return Math.Max(0, (access.DownloadExpiresAtUtc.Value.Date - now.Date).Days);
    }
}