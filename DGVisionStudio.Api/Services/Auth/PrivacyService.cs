using DGVisionStudio.Infrastructure.Data;

namespace DGVisionStudio.Api.Services;

public sealed class PrivacyService : IPrivacyService
{
    private readonly PrivacyExportQueryService exportQuery;
    private readonly PrivacyAnonymizationService anonymization;

    public PrivacyService(
        PrivacyExportQueryService exportQuery,
        PrivacyAnonymizationService anonymization)
    {
        this.exportQuery =
            exportQuery ?? throw new ArgumentNullException(nameof(exportQuery));
        this.anonymization =
            anonymization ?? throw new ArgumentNullException(nameof(anonymization));
    }

    public PrivacyService(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        exportQuery = new PrivacyExportQueryService(
            context,
            new PrivacyExportMapper());
        anonymization = new PrivacyAnonymizationService(
            context,
            new PrivacyAnonymizationMapper());
    }

    public Task<GdprExportResponse?> ExportUserDataAsync(string userId) =>
        exportQuery.ExportUserDataAsync(userId);

    public Task<bool> AnonymizeUserDataAsync(string userId) =>
        anonymization.AnonymizeUserDataAsync(userId);
}
