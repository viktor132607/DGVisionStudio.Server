using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Domain.Entities;

public class ApplicationUser : IdentityUser
{
	public bool IsBlocked { get; set; } = false;
	public bool IsSeenByAdmin { get; set; } = false;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}