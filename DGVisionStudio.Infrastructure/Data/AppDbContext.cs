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
	public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
	public DbSet<PrintRequest> PrintRequests => Set<PrintRequest>();
	public DbSet<PrintRequestItem> PrintRequestItems => Set<PrintRequestItem>();
	public DbSet<ShootingCalendarEvent> ShootingCalendarEvents => Set<ShootingCalendarEvent>();

	protected override void OnModelCreating(ModelBuilder builder)
	{
		base.OnModelCreating(builder);

		builder.Entity<ApplicationUser>()
			.Property(x => x.IsBlocked)
			.HasDefaultValue(false);

		builder.Entity<ApplicationUser>()
			.Property(x => x.IsSeenByAdmin)
			.HasDefaultValue(false);

		builder.Entity<ApplicationUser>()
			.Property(x => x.CreatedAtUtc)
			.HasDefaultValueSql("NOW()");

		builder.Entity<ContactRequest>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
			entity.Property(x => x.Email).IsRequired().HasMaxLength(150);
			entity.Property(x => x.Phone).HasMaxLength(30);
			entity.Property(x => x.Subject).HasMaxLength(200);
			entity.Property(x => x.Message).IsRequired().HasMaxLength(4000);
			entity.Property(x => x.AdminComment).HasMaxLength(2000);
			entity.Property(x => x.IsSeenByAdmin).HasDefaultValue(false);
			entity.HasIndex(x => x.CreatedAtUtc);
			entity.HasIndex(x => x.Status);
			entity.HasIndex(x => x.IsSeenByAdmin);
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
			entity.Property(x => x.IsDeleted).HasDefaultValue(false);

			entity.HasQueryFilter(x => !x.IsDeleted);

			entity.HasIndex(x => x.Key).IsUnique();
			entity.HasIndex(x => x.DisplayOrder);
			entity.HasIndex(x => x.IsActive);
			entity.HasIndex(x => x.IsDeleted);
			entity.HasIndex(x => x.DeletedAtUtc);
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
			entity.Property(x => x.IsUserUploaded).HasDefaultValue(false);
			entity.Property(x => x.IsSeenByAdmin).HasDefaultValue(false);
			entity.Property(x => x.OwnerUserId).HasMaxLength(450);
			entity.Property(x => x.GalleryType).HasConversion<int>();
			entity.Property(x => x.UserGalleryStatus).HasConversion<int>();
			entity.Property(x => x.IsDeleted).HasDefaultValue(false);

			entity.HasQueryFilter(x => !x.IsDeleted);

			entity.HasIndex(x => x.Slug).IsUnique();
			entity.HasIndex(x => x.PortfolioCategoryId);
			entity.HasIndex(x => x.DisplayOrder);
			entity.HasIndex(x => x.IsPublished);
			entity.HasIndex(x => x.AllowClientAccess);
			entity.HasIndex(x => x.GalleryType);
			entity.HasIndex(x => x.IsUserUploaded);
			entity.HasIndex(x => x.IsSeenByAdmin);
			entity.HasIndex(x => x.OwnerUserId);
			entity.HasIndex(x => x.ExpiresAtUtc);
			entity.HasIndex(x => x.UserGalleryStatus);
			entity.HasIndex(x => x.IsDeleted);
			entity.HasIndex(x => x.DeletedAtUtc);

			entity.HasOne(x => x.PortfolioCategory)
				.WithMany(x => x.Albums)
				.HasForeignKey(x => x.PortfolioCategoryId)
				.OnDelete(DeleteBehavior.Restrict);

			entity.HasOne(x => x.OwnerUser)
				.WithMany()
				.HasForeignKey(x => x.OwnerUserId)
				.OnDelete(DeleteBehavior.SetNull);
		});

		builder.Entity<PortfolioImage>(entity =>
		{
			entity.HasKey(x => x.Id);
			entity.Property(x => x.ImageUrl).IsRequired().HasMaxLength(1000);
			entity.Property(x => x.ThumbnailUrl).HasMaxLength(1000);
			entity.Property(x => x.AltText).HasMaxLength(300);
			entity.Property(x => x.Caption).HasMaxLength(1000);
			entity.Property(x => x.IsDeleted).HasDefaultValue(false);

			entity.HasQueryFilter(x => !x.IsDeleted);

			entity.HasIndex(x => x.PortfolioAlbumId);
			entity.HasIndex(x => x.DisplayOrder);
			entity.HasIndex(x => x.IsPublished);
			entity.HasIndex(x => x.IsCover);
			entity.HasIndex(x => x.IsDeleted);
			entity.HasIndex(x => x.DeletedAtUtc);

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

		builder.Entity<AuditLog>(entity =>
		{
			entity.HasKey(x => x.Id);

			entity.Property(x => x.AdminUserId).IsRequired().HasMaxLength(450);
			entity.Property(x => x.AdminEmail).IsRequired().HasMaxLength(150);
			entity.Property(x => x.Action).IsRequired().HasMaxLength(120);
			entity.Property(x => x.EntityType).IsRequired().HasMaxLength(120);
			entity.Property(x => x.EntityId).HasMaxLength(120);
			entity.Property(x => x.OldValue).HasColumnType("text");
			entity.Property(x => x.NewValue).HasColumnType("text");
			entity.Property(x => x.IpAddress).HasMaxLength(100);
			entity.Property(x => x.UserAgent).HasMaxLength(1000);
			entity.Property(x => x.TraceId).HasMaxLength(200);

			entity.HasIndex(x => x.CreatedAtUtc);
			entity.HasIndex(x => x.AdminUserId);
			entity.HasIndex(x => x.AdminEmail);
			entity.HasIndex(x => x.Action);
			entity.HasIndex(x => x.EntityType);
			entity.HasIndex(x => x.EntityId);
			entity.HasIndex(x => new { x.EntityType, x.EntityId });
		});

		builder.Entity<PrintRequest>(entity =>
		{
			entity.HasKey(x => x.Id);

			entity.Property(x => x.UserId).IsRequired().HasMaxLength(450);
			entity.Property(x => x.FullName).IsRequired().HasMaxLength(150);
			entity.Property(x => x.Email).IsRequired().HasMaxLength(150);
			entity.Property(x => x.Phone).HasMaxLength(50);
			entity.Property(x => x.Notes).HasMaxLength(2000);
			entity.Property(x => x.Status).IsRequired().HasMaxLength(40).HasDefaultValue("New");
			entity.Property(x => x.IsSeenByAdmin).HasDefaultValue(false);

			entity.HasIndex(x => x.UserId);
			entity.HasIndex(x => x.PortfolioAlbumId);
			entity.HasIndex(x => x.Status);
			entity.HasIndex(x => x.IsSeenByAdmin);
			entity.HasIndex(x => x.CreatedAtUtc);

			entity.HasOne(x => x.User)
				.WithMany()
				.HasForeignKey(x => x.UserId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(x => x.PortfolioAlbum)
				.WithMany()
				.HasForeignKey(x => x.PortfolioAlbumId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		builder.Entity<PrintRequestItem>(entity =>
		{
			entity.HasKey(x => x.Id);

			entity.Property(x => x.Quantity).HasDefaultValue(1);
			entity.Property(x => x.Size).IsRequired().HasMaxLength(50);
			entity.Property(x => x.PaperType).HasMaxLength(100);
			entity.Property(x => x.Notes).HasMaxLength(1000);

			entity.HasIndex(x => x.PrintRequestId);
			entity.HasIndex(x => x.PortfolioImageId);

			entity.HasOne(x => x.PrintRequest)
				.WithMany(x => x.Items)
				.HasForeignKey(x => x.PrintRequestId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(x => x.PortfolioImage)
				.WithMany()
				.HasForeignKey(x => x.PortfolioImageId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		builder.Entity<ShootingCalendarEvent>(entity =>
		{
			entity.HasKey(x => x.Id);

			entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
			entity.Property(x => x.EventType).HasMaxLength(40);
			entity.Property(x => x.AssignedTo).HasMaxLength(150);
			entity.Property(x => x.ClientName).HasMaxLength(150);
			entity.Property(x => x.ClientPhone).HasMaxLength(50);
			entity.Property(x => x.Location).HasMaxLength(300);
			entity.Property(x => x.Description).HasMaxLength(2000);
			entity.Property(x => x.Color).HasMaxLength(20);
			entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("NOW()");

			entity.HasIndex(x => x.StartAtUtc);
			entity.HasIndex(x => x.EndAtUtc);
			entity.HasIndex(x => x.EventType);
		});
	}
}
