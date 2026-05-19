namespace DGVisionStudio.Application.Interfaces;

public interface IClientGalleryExpiryService
{
	Task<int> MarkExpiredUserGalleriesAsync(CancellationToken cancellationToken = default);
	Task<int> DeleteExpiredUserGalleriesAsync(CancellationToken cancellationToken = default);
}
