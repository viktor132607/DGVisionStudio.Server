namespace DGVisionStudio.Application.Interfaces;

public interface IFileStorageService
{
	Task<string> SaveFileAsync(
		Stream fileStream,
		string fileName,
		string folderPath,
		CancellationToken cancellationToken = default);

	Task<string> SaveImageAsync(
		Stream fileStream,
		string fileName,
		string folderPath,
		int maxWidth = 2400,
		int quality = 82,
		CancellationToken cancellationToken = default);

	Task DeleteFileAsync(
		string relativePath,
		CancellationToken cancellationToken = default);

	Task<Stream?> OpenReadAsync(
		string relativePath,
		CancellationToken cancellationToken = default);

	Task<bool> FileExistsAsync(
		string relativePath,
		CancellationToken cancellationToken = default);
}