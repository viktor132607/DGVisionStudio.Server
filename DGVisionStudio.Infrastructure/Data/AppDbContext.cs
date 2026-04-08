using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DGVisionStudio.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
	{
	}

	public DbSet<ContactRequest> ContactRequests => Set<ContactRequest>();
	public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
	public DbSet<Service> Services => Set<Service>();
	public DbSet<Testimonial> Testimonials => Set<Testimonial>();
	public DbSet<PortfolioCategory> PortfolioCategories => Set<PortfolioCategory>();
	public DbSet<PortfolioAlbum> PortfolioAlbums => Set<PortfolioAlbum>();
	public DbSet<PortfolioImage> PortfolioImages => Set<PortfolioImage>();
	public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
	public DbSet<UserAlbumAccess> UserAlbumAccesses => Set<UserAlbumAccess>();

	protected override void OnModelCreating(ModelBuilder builder)
	{
		base.OnModelCreating(builder);

		builder.Entity<ApplicationUser>()
			.Property(x => x.IsBlocked)
			.HasDefaultValue(false);

		builder.Entity<ContactRequest>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
			entity.Property(x => x.Email).IsRequired().HasMaxLength(150);
			entity.Property(x => x.Phone).HasMaxLength(30);
			entity.Property(x => x.Subject).HasMaxLength(200);
			entity.Property(x => x.Message).IsRequired().HasMaxLength(4000);
			entity.Property(x => x.AdminComment).HasMaxLength(2000);
			entity.HasIndex(x => x.CreatedAtUtc);
			entity.HasIndex(x => x.Status);
		});

		builder.Entity<EmailLog>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.ToEmail).IsRequired().HasMaxLength(150);
			entity.Property(x => x.Subject).IsRequired().HasMaxLength(200);
			entity.Property(x => x.Body).IsRequired();
			entity.HasOne(x => x.ContactRequest)
				.WithMany()
				.HasForeignKey(x => x.ContactRequestId)
				.OnDelete(DeleteBehavior.SetNull);
		});

		builder.Entity<Service>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Title).IsRequired().HasMaxLength(150);
			entity.Property(x => x.Description).IsRequired().HasMaxLength(4000);
			entity.Property(x => x.ShortDescription).HasMaxLength(350);
			entity.Property(x => x.CoverImageUrl).HasMaxLength(1000);
			entity.HasIndex(x => x.DisplayOrder);
			entity.HasIndex(x => x.IsActive);
		});

		builder.Entity<Testimonial>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.ClientName).IsRequired().HasMaxLength(120);
			entity.Property(x => x.ClientCompany).HasMaxLength(150);
			entity.Property(x => x.ClientRole).HasMaxLength(150);
			entity.Property(x => x.Content).IsRequired().HasMaxLength(2000);
			entity.HasIndex(x => x.DisplayOrder);
			entity.HasIndex(x => x.IsPublished);
		});

		builder.Entity<PortfolioCategory>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Key).IsRequired().HasMaxLength(80);
			entity.Property(x => x.Name).IsRequired().HasMaxLength(120);
			entity.Property(x => x.NameEn).IsRequired().HasMaxLength(120);
			entity.Property(x => x.Description).HasMaxLength(1000);
			entity.HasIndex(x => x.Key).IsUnique();
			entity.HasIndex(x => x.DisplayOrder);
			entity.HasIndex(x => x.IsActive);
		});

		builder.Entity<PortfolioAlbum>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Slug).IsRequired().HasMaxLength(120);
			entity.Property(x => x.Title).IsRequired().HasMaxLength(150);
			entity.Property(x => x.TitleEn).HasMaxLength(150);
			entity.Property(x => x.Description).HasMaxLength(1500);
			entity.Property(x => x.CoverImageUrl).HasMaxLength(1000);
			entity.Property(x => x.AllowClientAccess).HasDefaultValue(true);
			entity.Property(x => x.IsPublished).HasDefaultValue(true);
			entity.HasIndex(x => x.Slug).IsUnique();
			entity.HasIndex(x => x.PortfolioCategoryId);
			entity.HasIndex(x => x.DisplayOrder);
			entity.HasIndex(x => x.IsPublished);
			entity.HasIndex(x => x.AllowClientAccess);
			entity.HasOne(x => x.PortfolioCategory)
				.WithMany(x => x.Albums)
				.HasForeignKey(x => x.PortfolioCategoryId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		builder.Entity<PortfolioImage>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.ImageUrl).IsRequired().HasMaxLength(1000);
			entity.Property(x => x.ThumbnailUrl).HasMaxLength(1000);
			entity.Property(x => x.AltText).HasMaxLength(300);
			entity.Property(x => x.Caption).HasMaxLength(1000);
			entity.HasIndex(x => x.PortfolioAlbumId);
			entity.HasIndex(x => x.DisplayOrder);
			entity.HasIndex(x => x.IsPublished);
			entity.HasIndex(x => x.IsCover);
			entity.HasOne(x => x.PortfolioAlbum)
				.WithMany(x => x.Images)
				.HasForeignKey(x => x.PortfolioAlbumId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		builder.Entity<SiteSetting>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Key).IsRequired().HasMaxLength(120);
			entity.Property(x => x.Value).IsRequired().HasMaxLength(4000);
			entity.Property(x => x.Description).HasMaxLength(1000);
			entity.HasIndex(x => x.Key).IsUnique();
		});

		builder.Entity<UserAlbumAccess>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.UserId).IsRequired();
			entity.Property(x => x.PreviewEnabled).HasDefaultValue(true);
			entity.Property(x => x.DownloadEnabled).HasDefaultValue(false);

			entity.HasIndex(x => x.PortfolioAlbumId);
			entity.HasIndex(x => x.UserId);
			entity.HasIndex(x => new { x.PortfolioAlbumId, x.UserId }).IsUnique();

			entity.HasOne(x => x.PortfolioAlbum)
				.WithMany(x => x.UserAccesses)
				.HasForeignKey(x => x.PortfolioAlbumId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(x => x.User)
				.WithMany()
				.HasForeignKey(x => x.UserId)
				.OnDelete(DeleteBehavior.Cascade);
		});
	}
}