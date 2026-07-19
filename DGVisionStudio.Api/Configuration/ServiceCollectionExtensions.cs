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

        services.AddScoped<HomeSlideshowSettingsService>();
        services.AddScoped<HomeSlideshowVideoService>();
        services.AddScoped<HomeSlideshowImageService>();
        services.AddScoped<IHomeSlideshowService>(serviceProvider =>
            new HomeSlideshowService(
                serviceProvider.GetRequiredService<HomeSlideshowImageService>(),
                serviceProvider.GetRequiredService<HomeSlideshowSettingsService>(),
                serviceProvider.GetRequiredService<HomeSlideshowVideoService>()));

        services.AddScoped<IPrivacyService, PrivacyService>();

        services.AddScoped<AuthRegistrationService>();
        services.AddScoped<AuthSessionService>();
        services.AddScoped<AuthPasswordService>();
        services.AddScoped<IAuthService>(serviceProvider =>
            new AuthService(
                serviceProvider.GetRequiredService<AuthRegistrationService>(),
                serviceProvider.GetRequiredService<AuthSessionService>(),
                serviceProvider.GetRequiredService<AuthPasswordService>()));

        services.AddScoped<PortfolioCategoryAdminService>();
        services.AddScoped<PortfolioAlbumAdminService>();
        services.AddScoped<PortfolioImageAdminService>();
        services.AddScoped<IAdminPortfolioService>(serviceProvider =>
            new AdminPortfolioService(
                serviceProvider.GetRequiredService<PortfolioCategoryAdminService>(),
                serviceProvider.GetRequiredService<PortfolioAlbumAdminService>(),
                serviceProvider.GetRequiredService<PortfolioImageAdminService>()));

        services.AddScoped<AdminGalleryMediaMetadataService>();
        services.AddScoped<AdminGalleryMediaDownloadService>();
        services.AddScoped<AdminGalleryMediaUploadService>();
        services.AddScoped<AdminGalleryMediaMutationService>();
        services.AddScoped<IAdminGalleryMediaManagementService>(serviceProvider =>
            new AdminGalleryMediaManagementService(
                serviceProvider.GetRequiredService<AdminGalleryMediaMetadataService>(),
                serviceProvider.GetRequiredService<AdminGalleryMediaDownloadService>(),
                serviceProvider.GetRequiredService<AdminGalleryMediaUploadService>(),
                serviceProvider.GetRequiredService<AdminGalleryMediaMutationService>()));

        services.AddScoped<AdminPrintRequestQueryService>();
        services.AddScoped<AdminPrintRequestCommandService>();
        services.AddScoped<IAdminPrintRequestService>(serviceProvider =>
            new AdminPrintRequestService(
                serviceProvider.GetRequiredService<AdminPrintRequestQueryService>(),
                serviceProvider.GetRequiredService<AdminPrintRequestCommandService>()));

        services.AddScoped<AdminClientGalleryQueryService>();
        services.AddScoped<AdminClientGalleryCommandService>();
        services.AddScoped<AdminClientGalleryExportService>();
        services.AddScoped<IAdminClientGalleryManagementService>(serviceProvider =>
            new AdminClientGalleryManagementService(
                serviceProvider.GetRequiredService<AdminClientGalleryQueryService>(),
                serviceProvider.GetRequiredService<AdminClientGalleryCommandService>(),
                serviceProvider.GetRequiredService<AdminClientGalleryExportService>()));

        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IClientGalleryEndpointService, ClientGalleryEndpointService>();
        services.AddScoped<IAdminCalendarService, AdminCalendarService>();
        services.AddScoped<IContactRequestService, ContactRequestService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IPortfolioQueryService, PortfolioQueryService>();
        services.AddScoped<IAdminStatisticsService, AdminStatisticsService>();
        services.AddScoped<ITestimonialService, TestimonialService>();
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IPrivacyEndpointService, PrivacyEndpointService>();
        services.AddScoped<IAccountEndpointService, AccountEndpointService>();
        services.AddScoped<IAdminAuditLogQueryService, AdminAuditLogQueryService>();
        services.AddScoped<IAdminGalleryArchiveService, AdminGalleryArchiveService>();
        services.AddScoped<IAdminGalleryAccessEndpointService, AdminGalleryAccessEndpointService>();
        services.AddScoped<IClientPrintRequestEndpointService, ClientPrintRequestEndpointService>();
        services.AddScoped<ICsrfTokenService, CsrfTokenService>();
        services.AddScoped<IDebugUserService, DebugUserService>();
        services.AddScoped<ISiteSettingsService, SiteSettingsService>();
        services.AddScoped<IHomeStatusService, HomeStatusService>();

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

        services.AddScoped<ClientGalleryAdminQueryService>();
        services.AddScoped<ClientGalleryAdminCommandService>();
        services.AddScoped<IClientGalleryAdminService>(serviceProvider =>
            new ClientGalleryAdminService(
                serviceProvider.GetRequiredService<ClientGalleryAdminQueryService>(),
                serviceProvider.GetRequiredService<ClientGalleryAdminCommandService>()));

        services.AddScoped<ClientGalleryUserQueryService>();
        services.AddScoped<ClientGalleryUserCreationService>();
        services.AddScoped<ClientGalleryUserLifecycleService>();
        services.AddScoped<IClientGalleryUserService>(serviceProvider =>
            new ClientGalleryUserService(
                serviceProvider.GetRequiredService<ClientGalleryUserQueryService>(),
                serviceProvider.GetRequiredService<ClientGalleryUserCreationService>(),
                serviceProvider.GetRequiredService<ClientGalleryUserLifecycleService>()));

        services.AddScoped<IClientGalleryAccessService, ClientGalleryAccessService>();

        services.AddScoped<ClientGalleryPhotoDownloadService>();
        services.AddScoped<ClientGalleryPhotoUploadService>();
        services.AddScoped<ClientGalleryPhotoMutationService>();
        services.AddScoped<IClientGalleryPhotoService>(serviceProvider =>
            new ClientGalleryPhotoService(
                serviceProvider.GetRequiredService<ClientGalleryPhotoDownloadService>(),
                serviceProvider.GetRequiredService<ClientGalleryPhotoUploadService>(),
                serviceProvider.GetRequiredService<ClientGalleryPhotoMutationService>()));

        services.AddScoped<IClientGalleryExpiryService, ClientGalleryExpiryService>();

        services.AddScoped<ClientGalleryMapper>();
        services.AddScoped<ClientGalleryUploadValidator>();
        services.AddScoped<ClientGalleryNamingService>();

        services.AddHostedService<ExpiredGalleryCleanupService>();
        services.AddHostedService<CalendarReminderEmailService>();

        return services;
    }
}
