using DGVisionStudio.Api.Services;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDGVisionApplicationServices(
        this IServiceCollection services,
        StorageOptions storageOptions)
    {
        services.AddScoped<PhotographyPageService>();
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

        services.AddScoped<PrivacyExportMapper>();
        services.AddScoped<PrivacyExportQueryService>();
        services.AddScoped<PrivacyAnonymizationMapper>();
        services.AddScoped<PrivacyAnonymizationService>();
        services.AddScoped<IPrivacyService>(serviceProvider =>
            new PrivacyService(
                serviceProvider.GetRequiredService<PrivacyExportQueryService>(),
                serviceProvider.GetRequiredService<PrivacyAnonymizationService>()));

        services.AddScoped<AuthRegistrationService>();
        services.AddScoped<AuthSessionService>();
        services.AddScoped<AuthPasswordResetLinkService>();
        services.AddScoped<AuthForgotPasswordService>();
        services.AddScoped<AuthResetPasswordService>();
        services.AddScoped<AuthChangePasswordService>();
        services.AddScoped<AuthPasswordService>(serviceProvider =>
            new AuthPasswordService(
                serviceProvider.GetRequiredService<AuthForgotPasswordService>(),
                serviceProvider.GetRequiredService<AuthPasswordResetLinkService>(),
                serviceProvider.GetRequiredService<AuthResetPasswordService>(),
                serviceProvider.GetRequiredService<AuthChangePasswordService>()));
        services.AddScoped<IAuthService>(serviceProvider =>
            new AuthService(
                serviceProvider.GetRequiredService<AuthRegistrationService>(),
                serviceProvider.GetRequiredService<AuthSessionService>(),
                serviceProvider.GetRequiredService<AuthPasswordService>()));

        services.AddScoped<PortfolioCategoryAuditService>();
        services.AddScoped<PortfolioCategoryQueryService>();
        services.AddScoped<PortfolioCategoryOrderingService>();
        services.AddScoped<PortfolioCategoryAlbumAssignmentService>();
        services.AddScoped<PortfolioCategoryCommandService>();
        services.AddScoped<PortfolioCategoryAdminService>(serviceProvider =>
            new PortfolioCategoryAdminService(
                serviceProvider.GetRequiredService<PortfolioCategoryQueryService>(),
                serviceProvider.GetRequiredService<PortfolioCategoryCommandService>(),
                serviceProvider.GetRequiredService<PortfolioCategoryOrderingService>(),
                serviceProvider.GetRequiredService<PortfolioCategoryAlbumAssignmentService>()));
        services.AddScoped<PortfolioAlbumAuditService>();
        services.AddScoped<PortfolioAlbumInputValidator>();
        services.AddScoped<PortfolioAlbumMapper>();
        services.AddScoped<PortfolioAlbumQueryService>();
        services.AddScoped<PortfolioAlbumCommandService>();
        services.AddScoped<PortfolioAlbumAdminService>(serviceProvider =>
            new PortfolioAlbumAdminService(
                serviceProvider.GetRequiredService<PortfolioAlbumQueryService>(),
                serviceProvider.GetRequiredService<PortfolioAlbumCommandService>()));
        services.AddScoped<PortfolioImageAuditService>();
        services.AddScoped<PortfolioImageInputValidator>();
        services.AddScoped<PortfolioImageMapper>();
        services.AddScoped<PortfolioImageQueryService>();
        services.AddScoped<PortfolioImageCommandService>();
        services.AddScoped<PortfolioImageAdminService>(serviceProvider =>
            new PortfolioImageAdminService(
                serviceProvider.GetRequiredService<PortfolioImageQueryService>(),
                serviceProvider.GetRequiredService<PortfolioImageCommandService>()));
        services.AddScoped<IAdminPortfolioService>(serviceProvider =>
            new AdminPortfolioService(
                serviceProvider.GetRequiredService<PortfolioCategoryAdminService>(),
                serviceProvider.GetRequiredService<PortfolioAlbumAdminService>(),
                serviceProvider.GetRequiredService<PortfolioImageAdminService>()));

        services.AddScoped<AdminGalleryMediaMetadataService>();
        services.AddScoped<AdminGalleryMediaDownloadService>();
        services.AddScoped<AdminGalleryVideoFileStorageService>();
        services.AddScoped<AdminGalleryPhotoUploadService>();
        services.AddScoped<AdminGalleryVideoUploadService>();
        services.AddScoped<AdminGalleryMediaUploadService>(serviceProvider =>
            new AdminGalleryMediaUploadService(
                serviceProvider.GetRequiredService<AdminGalleryPhotoUploadService>(),
                serviceProvider.GetRequiredService<AdminGalleryVideoUploadService>()));
        services.AddScoped<AdminGalleryMediaMutationService>();
        services.AddScoped<IAdminGalleryMediaManagementService>(serviceProvider =>
            new AdminGalleryMediaManagementService(
                serviceProvider.GetRequiredService<AdminGalleryMediaMetadataService>(),
                serviceProvider.GetRequiredService<AdminGalleryMediaDownloadService>(),
                serviceProvider.GetRequiredService<AdminGalleryMediaUploadService>(),
                serviceProvider.GetRequiredService<AdminGalleryMediaMutationService>()));

        services.AddScoped<AdminPrintRequestMapper>();
        services.AddScoped<AdminDirectPrintRequestQueryService>();
        services.AddScoped<AdminUploadedPrintRequestQueryService>();
        services.AddScoped<AdminPrintRequestQueryService>(serviceProvider =>
            new AdminPrintRequestQueryService(
                serviceProvider.GetRequiredService<AdminDirectPrintRequestQueryService>(),
                serviceProvider.GetRequiredService<AdminUploadedPrintRequestQueryService>()));
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

        services.AddScoped<AdminUserProtectedAccountPolicy>();
        services.AddScoped<AdminUserQueryService>();
        services.AddScoped<AdminUserSeenService>();
        services.AddScoped<AdminUserRoleService>();
        services.AddScoped<AdminUserAccountService>();
        services.AddScoped<IAdminUserService>(serviceProvider =>
            new AdminUserService(
                serviceProvider.GetRequiredService<AdminUserQueryService>(),
                serviceProvider.GetRequiredService<AdminUserSeenService>(),
                serviceProvider.GetRequiredService<AdminUserRoleService>(),
                serviceProvider.GetRequiredService<AdminUserAccountService>()));
        services.AddScoped<ClientGalleryEndpointUserContextService>();
        services.AddScoped<ClientGalleryUserEndpointService>();
        services.AddScoped<ClientGalleryPhotoDownloadEndpointService>();
        services.AddScoped<ClientGalleryZipDownloadService>();
        services.AddScoped<IClientGalleryEndpointService>(serviceProvider =>
            new ClientGalleryEndpointService(
                serviceProvider.GetRequiredService<ClientGalleryUserEndpointService>(),
                serviceProvider.GetRequiredService<ClientGalleryPhotoDownloadEndpointService>(),
                serviceProvider.GetRequiredService<ClientGalleryZipDownloadService>()));
        services.AddScoped<AdminCalendarQueryService>();
        services.AddScoped<AdminCalendarContactRequestQueryService>();
        services.AddScoped<AdminCalendarEventInputService>();
        services.AddScoped<AdminCalendarCommandService>();
        services.AddScoped<IAdminCalendarService>(serviceProvider =>
            new AdminCalendarService(
                serviceProvider.GetRequiredService<AdminCalendarQueryService>(),
                serviceProvider.GetRequiredService<AdminCalendarContactRequestQueryService>(),
                serviceProvider.GetRequiredService<AdminCalendarCommandService>()));
        services.AddScoped<ContactRequestInputValidator>();
        services.AddScoped<ContactRequestFactory>();
        services.AddScoped<ContactRequestNotificationService>(serviceProvider =>
            new ContactRequestNotificationService(
                serviceProvider.GetRequiredService<AppDbContext>(),
                serviceProvider.GetRequiredService<IEmailService>(),
                serviceProvider.GetRequiredService<IConfiguration>()));
        services.AddScoped<ContactRequestSubmissionService>();
        services.AddScoped<ContactRequestQueryService>();
        services.AddScoped<ContactRequestAdminCommandService>();
        services.AddScoped<IContactRequestService>(serviceProvider =>
            new ContactRequestService(
                serviceProvider.GetRequiredService<ContactRequestSubmissionService>(),
                serviceProvider.GetRequiredService<ContactRequestQueryService>(),
                serviceProvider.GetRequiredService<ContactRequestAdminCommandService>()));
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IPortfolioQueryService, PortfolioQueryService>();
        services.AddScoped<IAdminStatisticsService, AdminStatisticsService>();
        services.AddScoped<ITestimonialService, TestimonialService>();
        services.AddScoped<IHealthService, HealthService>();
        services.AddScoped<IPrivacyEndpointService, PrivacyEndpointService>();
        services.AddScoped<IAccountEndpointService, AccountEndpointService>();
        services.AddScoped<IAdminAuditLogQueryService, AdminAuditLogQueryService>();
        services.AddScoped<IAdminGalleryArchiveService, AdminGalleryArchiveService>();
        services.AddScoped<PortfolioArchiveSelectionService>();
        services.AddScoped<PortfolioArchiveNameService>();
        services.AddScoped<PortfolioArchivePhotoWriter>();
        services.AddScoped<PortfolioArchiveZipWriter>();
        services.AddScoped<PortfolioArchiveVerifier>();
        services.AddScoped<PortfolioArchiveBuilder>(serviceProvider =>
            new PortfolioArchiveBuilder(
                serviceProvider.GetRequiredService<PortfolioArchiveSelectionService>(),
                serviceProvider.GetRequiredService<PortfolioArchiveZipWriter>(),
                serviceProvider.GetRequiredService<PortfolioArchiveVerifier>()));
        services.AddScoped<PortfolioAlbumBulkService>();
        services.AddSingleton<PortfolioArchiveJobs>();
        services.AddHostedService(sp => sp.GetRequiredService<PortfolioArchiveJobs>());
        services.AddScoped<IAdminGalleryAccessEndpointService, AdminGalleryAccessEndpointService>();
        services.AddScoped<IClientPrintRequestEndpointService, ClientPrintRequestEndpointService>();
        services.AddScoped<ICsrfTokenService, CsrfTokenService>();
        services.AddScoped<IDebugUserService, DebugUserService>();
        services.AddScoped<ISiteSettingsService, SiteSettingsService>();
        services.AddScoped<IHomeStatusService, HomeStatusService>();

        if (storageOptions.UseCloudinary)
        {
            services.AddScoped<ICloudinaryStorageClient, CloudinaryStorageClient>();
            services.AddScoped<CloudinaryImageOptimizer>();
            services.AddScoped<CloudinaryPathService>(serviceProvider =>
                new CloudinaryPathService(
                    serviceProvider.GetRequiredService<IConfiguration>()));
            services.AddScoped<IFileStorageService>(serviceProvider =>
                new CloudinaryFileStorageService(
                    serviceProvider.GetRequiredService<ICloudinaryStorageClient>(),
                    serviceProvider.GetRequiredService<CloudinaryImageOptimizer>(),
                    serviceProvider.GetRequiredService<CloudinaryPathService>()));
        }
        else
        {
            services.AddScoped<FileStoragePathService>();
            services.AddScoped<FileStorageFileService>();
            services.AddScoped<FileStorageImageService>();
            services.AddScoped<IFileStorageService>(serviceProvider =>
                new FileStorageService(
                    serviceProvider.GetRequiredService<FileStorageFileService>(),
                    serviceProvider.GetRequiredService<FileStorageImageService>()));
        }

        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddScoped<IClientGalleryService, ClientGalleryService>();

        services.AddScoped<ClientGalleryAdminQueryService>();
        services.AddScoped<ClientGalleryAdminInputNormalizer>();
        services.AddScoped<ClientGalleryAdminCategoryService>();
        services.AddScoped<ClientGalleryAdminAlbumMapper>();
        services.AddScoped<ClientGalleryAdminCreateService>();
        services.AddScoped<ClientGalleryAdminUpdateService>();
        services.AddScoped<ClientGalleryAdminLifecycleService>();
        services.AddScoped<ClientGalleryAdminCommandService>(serviceProvider =>
            new ClientGalleryAdminCommandService(
                serviceProvider.GetRequiredService<ClientGalleryAdminCreateService>(),
                serviceProvider.GetRequiredService<ClientGalleryAdminUpdateService>(),
                serviceProvider.GetRequiredService<ClientGalleryAdminLifecycleService>()));
        services.AddScoped<IClientGalleryAdminService>(serviceProvider =>
            new ClientGalleryAdminService(
                serviceProvider.GetRequiredService<ClientGalleryAdminQueryService>(),
                serviceProvider.GetRequiredService<ClientGalleryAdminCommandService>()));

        services.AddScoped<ClientGalleryUserQueryService>();
        services.AddScoped<ClientGalleryUserAlbumCreationService>();
        services.AddScoped<ClientGalleryUserPhotoUploadService>();
        services.AddScoped<ClientGalleryUserCreationService>(serviceProvider =>
            new ClientGalleryUserCreationService(
                serviceProvider.GetRequiredService<ClientGalleryUserAlbumCreationService>(),
                serviceProvider.GetRequiredService<ClientGalleryUserPhotoUploadService>()));
        services.AddScoped<ClientGalleryUserLifecycleService>();
        services.AddScoped<IClientGalleryUserService>(serviceProvider =>
            new ClientGalleryUserService(
                serviceProvider.GetRequiredService<ClientGalleryUserQueryService>(),
                serviceProvider.GetRequiredService<ClientGalleryUserCreationService>(),
                serviceProvider.GetRequiredService<ClientGalleryUserLifecycleService>()));

        services.AddScoped<ClientGalleryAccessQueryService>();
        services.AddScoped<ClientGalleryAccessGrantService>();
        services.AddScoped<ClientGalleryAccessMutationService>();
        services.AddScoped<ClientGalleryAccessSyncService>();
        services.AddScoped<IClientGalleryAccessService>(serviceProvider =>
            new ClientGalleryAccessService(
                serviceProvider.GetRequiredService<ClientGalleryAccessQueryService>(),
                serviceProvider.GetRequiredService<ClientGalleryAccessGrantService>(),
                serviceProvider.GetRequiredService<ClientGalleryAccessMutationService>(),
                serviceProvider.GetRequiredService<ClientGalleryAccessSyncService>()));

        services.AddScoped<ClientGalleryPhotoDownloadService>();
        services.AddScoped<ClientGalleryPhotoUploadService>();
        services.AddScoped<ClientGalleryPhotoCoverService>();
        services.AddScoped<ClientGalleryPhotoUpdateService>();
        services.AddScoped<ClientGalleryPhotoDeleteService>();
        services.AddScoped<ClientGalleryPhotoReorderService>();
        services.AddScoped<ClientGalleryPhotoMutationService>(serviceProvider =>
            new ClientGalleryPhotoMutationService(
                serviceProvider.GetRequiredService<ClientGalleryPhotoUpdateService>(),
                serviceProvider.GetRequiredService<ClientGalleryPhotoDeleteService>(),
                serviceProvider.GetRequiredService<ClientGalleryPhotoCoverService>(),
                serviceProvider.GetRequiredService<ClientGalleryPhotoReorderService>()));
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
        services.AddScoped<CalendarReminderDueQueryService>();
        services.AddScoped<CalendarReminderMessageComposer>();
        services.AddScoped<CalendarReminderDeliveryService>();
        services.AddScoped<CalendarReminderProcessor>();
        services.AddHostedService<CalendarReminderEmailService>();

        return services;
    }
}
