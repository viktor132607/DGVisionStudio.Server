using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Controllers;

namespace DGVisionStudio.Api.Services;

public sealed class ServiceCatalogInputService
{
    public string? Validate(ServiceCardDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return "Заглавието е задължително.";

        if (string.IsNullOrWhiteSpace(dto.Description))
            return "Описанието е задължително.";

        return null;
    }

    public Service Create(
        ServiceCardDto dto,
        int displayOrder,
        DateTime createdAtUtc) =>
        new()
        {
            Title = dto.Title.Trim(),
            ShortDescription = Normalize(dto.ShortDescription),
            Description = dto.Description.Trim(),
            CoverImageUrl = Normalize(dto.CoverImageUrl),
            DisplayOrder = displayOrder,
            IsActive = dto.IsActive,
            CreatedAtUtc = createdAtUtc
        };

    public void Apply(Service item, ServiceCardDto dto)
    {
        item.Title = dto.Title.Trim();
        item.ShortDescription = Normalize(dto.ShortDescription);
        item.Description = dto.Description.Trim();
        item.CoverImageUrl = Normalize(dto.CoverImageUrl);
        item.IsActive = dto.IsActive;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
