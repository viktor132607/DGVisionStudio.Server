namespace DGVisionStudio.Domain.Entities;

public class Testimonial
{
    public int Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientCompany { get; set; }
    public string? ClientRole { get; set; }
    public string Content { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public bool IsPublished { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
