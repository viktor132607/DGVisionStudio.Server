using DGVisionStudio.Api.Services;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDGVisionApplicationServices(
        this IServiceCollection services,
        StorageOptions storageOptions)
    {
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IHomeSlideshowService, HomeSlideshowService>();
        services.AddScoped<IPrivacyService, PrivacyService>();

        if (storageOptions.UseCloudinary)
        {
            services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();
        }
        else
        {
            services.AddScoped<IFileStorageService, FileStorageService>();
        }

        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddScoped<IClientGalleryService, ClientGalleryService>();
        services.AddScoped<IClientGalleryAdminService, ClientGalleryAdminService>();
        services.AddScoped<IClientGalleryUserService, ClientGalleryUserService>();
        services.AddScoped<IClientGalleryAccessService, ClientGalleryAccessService>();
        services.AddScoped<IClientGalleryPhotoService, ClientGalleryPhotoService>();
        services.AddScoped<IClientGalleryExpiryService, ClientGalleryExpiryService>();

        services.AddScoped<ClientGalleryMapper>();
        services.AddScoped<ClientGalleryUploadValidator>();
        services.AddScoped<ClientGalleryNamingService>();

        services.AddHostedService<ExpiredGalleryCleanupService>();
        services.AddHostedService<CalendarReminderEmailService>();

        return services;
    }
}
