using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Api.Services;

public sealed class AdminGalleryVideoFileStorageService(IWebHostEnvironment environment)
{
    public async Task<string> SaveAsync(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var safeFileName = $"{Guid.NewGuid():N}{extension}";
        var uploadFolder = GetUploadFolder();

        Directory.CreateDirectory(uploadFolder);

        var fullPath = Path.Combine(uploadFolder, safeFileName);
        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/portfolio/videos/{safeFileName}";
    }

    private string GetUploadFolder()
    {
        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRootPath))
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");

        return Path.Combine(webRootPath, "uploads", "portfolio", "videos");
    }
}
