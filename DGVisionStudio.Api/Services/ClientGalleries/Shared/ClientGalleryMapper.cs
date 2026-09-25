using DGVisionStudio.Application.DTOs.ClientGalleries;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Infrastructure.Services.ClientGalleries;

public class ClientGalleryMapper
{
    private readonly ClientGalleryAccessPolicy _accessPolicy;
    private readonly ClientGalleryPhotoMapper _photoMapper;
    private readonly ClientGallerySummaryMapper _summaryMapper;
    private readonly ClientGalleryDetailsMapper _detailsMapper;

    [ActivatorUtilitiesConstructor]
    public ClientGalleryMapper(
        ClientGalleryAccessPolicy accessPolicy,
        ClientGalleryPhotoMapper photoMapper,
        ClientGallerySummaryMapper summaryMapper,
        ClientGalleryDetailsMapper detailsMapper)
    {
        _accessPolicy = accessPolicy;
        _photoMapper = photoMapper;
        _summaryMapper = summaryMapper;
        _detailsMapper = detailsMapper;
    }

    public ClientGalleryMapper()
        : this(
            new ClientGalleryAccessPolicy(),
            new ClientGalleryPhotoMapper(),
            new ClientGallerySummaryMapper(
                new ClientGalleryAccessPolicy()),
            new ClientGalleryDetailsMapper(
                new ClientGalleryAccessPolicy(),
                new ClientGalleryPhotoMapper()))
    {
    }

    public MyClientGalleryDto MapGalleryDto(
        PortfolioAlbum album,
        DateTime now,
        UserAlbumAccess? access,
        bool isOwner = false,
        bool isAdminView = false) =>
        _summaryMapper.Map(
            album,
            now,
            access,
            isOwner,
            isAdminView);

    public ClientGalleryDetailsDto MapGalleryDetailsDto(
        PortfolioAlbum album,
        DateTime now,
        UserAlbumAccess? access,
        bool canDownload,
        bool isOwner = false,
        bool isAdminView = false) =>
        _detailsMapper.Map(
            album,
            now,
            access,
            canDownload,
            isOwner,
            isAdminView);

    public ClientPhotoDto MapPhotoDto(
        PortfolioImage image,
        bool canDownload,
        int galleryId) =>
        _photoMapper.Map(
            image,
            canDownload,
            galleryId);

    public bool IsDownloadActive(
        UserAlbumAccess access,
        DateTime now) =>
        _accessPolicy.IsDownloadActive(access, now);

    public bool IsExpired(
        UserAlbumAccess access,
        DateTime now) =>
        _accessPolicy.IsExpired(access, now);

    public bool IsUserGalleryExpired(
        PortfolioAlbum album,
        DateTime now) =>
        _accessPolicy.IsUserGalleryExpired(album, now);

    public UserClientGalleryStatus GetEffectiveStatus(
        PortfolioAlbum album,
        DateTime now) =>
        _accessPolicy.GetEffectiveStatus(album, now);
}
