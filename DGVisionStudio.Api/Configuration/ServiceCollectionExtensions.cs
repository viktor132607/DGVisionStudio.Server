using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
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

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminPortfolioService, AdminPortfolioService>();
        services.AddScoped<IAdminClientGalleryManagementService, AdminClientGalleryManagementService>();
        services.AddScoped<IAdminPrintRequestService, AdminPrintRequestService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IClientGalleryEndpointService, ClientGalleryEndpointService>();
        services.AddScoped<IAdminCalendarService, AdminCalendarService>();
        services.AddScoped<IContactRequestService, ContactRequestService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IPortfolioQueryService, PortfolioQueryService>();
        services.AddScoped<IAdminStatisticsService, AdminStatisticsService>();
        services.AddScoped<ITestimonialService, TestimonialService>();
        services.AddScoped<IHealthService, HealthService>();

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
