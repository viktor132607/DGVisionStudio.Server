using DGVisionStudio.Api.Middleware;
using DGVisionStudio.Application.Interfaces;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services;
using DGVisionStudio.Infrastructure.Services.ClientGalleries;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

const long MaxUploadSizeBytes = 20 * 1024 * 1024;
const int MaxFilesPerUploadRequest = 100;
const long MaxUploadRequestBytes = MaxUploadSizeBytes * MaxFilesPerUploadRequest;

Log.Logger = new LoggerConfiguration()
	.ReadFrom.Configuration(builder.Configuration)
	.Enrich.FromLogContext()
	.Enrich.WithProperty("Application", "DGVisionStudio.Api")
	.WriteTo.Console()
	.WriteTo.File(
		path: "logs/dgvisionstudio-.log",
		rollingInterval: RollingInterval.Day,
		retainedFileCountLimit: 14,
		shared: true)
	.CreateLogger();

builder.Host.UseSerilog();

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.WebHost.ConfigureKestrel(options =>
{
	options.Limits.MaxRequestBodySize = MaxUploadRequestBytes;
});

builder.Services.AddControllers()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
	});

builder.Services.Configure<FormOptions>(options =>
{
	options.MultipartBodyLengthLimit = MaxUploadRequestBytes;
	options.ValueLengthLimit = int.MaxValue;
	options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IEmailService, EmailService>();

var storageProvider = builder.Configuration["Storage:Provider"];

if (string.Equals(storageProvider, "Cloudinary", StringComparison.OrdinalIgnoreCase))
{
	builder.Services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();
}
else
{
	builder.Services.AddScoped<IFileStorageService, FileStorageService>();
}

builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddScoped<IClientGalleryService, ClientGalleryService>();
builder.Services.AddScoped<IClientGalleryAdminService, ClientGalleryAdminService>();
builder.Services.AddScoped<IClientGalleryUserService, ClientGalleryUserService>();
builder.Services.AddScoped<IClientGalleryAccessService, ClientGalleryAccessService>();
builder.Services.AddScoped<IClientGalleryPhotoService, ClientGalleryPhotoService>();
builder.Services.AddScoped<IClientGalleryExpiryService, ClientGalleryExpiryService>();

builder.Services.AddScoped<ClientGalleryMapper>();
builder.Services.AddScoped<ClientGalleryUploadValidator>();
builder.Services.AddScoped<ClientGalleryNamingService>();

builder.Services.AddHostedService<ExpiredGalleryCleanupService>();

builder.Services.AddDbContext<AppDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
	.AddIdentity<ApplicationUser, IdentityRole>(options =>
	{
		options.Password.RequireDigit = true;
		options.Password.RequireLowercase = true;
		options.Password.RequireUppercase = true;
		options.Password.RequireNonAlphanumeric = true;
		options.Password.RequiredLength = 8;
		options.User.RequireUniqueEmail = true;

		options.SignIn.RequireConfirmedEmail = false;

		options.Lockout.AllowedForNewUsers = true;
		options.Lockout.MaxFailedAccessAttempts = 5;
		options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
	})
	.AddEntityFrameworkStores<AppDbContext>()
	.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
	options.Cookie.HttpOnly = true;
	options.Cookie.Name = "DGVisionStudio.Auth";
	options.Cookie.SameSite = SameSiteMode.None;
	options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
	options.Cookie.IsEssential = true;
	options.LoginPath = "/identity/login";
	options.AccessDeniedPath = "/identity/login";
	options.SlidingExpiration = true;

	options.Events.OnRedirectToLogin = context =>
	{
		if (context.Request.Path.StartsWithSegments("/api"))
		{
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			return Task.CompletedTask;
		}

		context.Response.Redirect(context.RedirectUri);
		return Task.CompletedTask;
	};

	options.Events.OnRedirectToAccessDenied = context =>
	{
		if (context.Request.Path.StartsWithSegments("/api"))
		{
			context.Response.StatusCode = StatusCodes.Status403Forbidden;
			return Task.CompletedTask;
		}

		context.Response.Redirect(context.RedirectUri);
		return Task.CompletedTask;
	};
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
	options.KnownIPNetworks.Clear();
	options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

	options.AddFixedWindowLimiter("auth", limiter =>
	{
		limiter.PermitLimit = 5;
		limiter.Window = TimeSpan.FromMinutes(1);
		limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
		limiter.QueueLimit = 0;
	});

	options.AddFixedWindowLimiter("contact", limiter =>
	{
		limiter.PermitLimit = 3;
		limiter.Window = TimeSpan.FromMinutes(5);
		limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
		limiter.QueueLimit = 0;
	});

	options.AddFixedWindowLimiter("upload", limiter =>
	{
		limiter.PermitLimit = 200;
		limiter.Window = TimeSpan.FromMinutes(10);
		limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
		limiter.QueueLimit = 0;
	});
});

var frontendUrl = builder.Configuration["Frontend:Url"]?.TrimEnd('/');

var allowedOrigins = new List<string>();

if (!string.IsNullOrWhiteSpace(frontendUrl))
{
	allowedOrigins.Add(frontendUrl);
}

builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowFrontend", policy =>
	{
		policy.WithOrigins(allowedOrigins.ToArray())
			.AllowAnyHeader()
			.AllowAnyMethod()
			.AllowCredentials();
	});
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseForwardedHeaders();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
	app.UseHsts();
}

var webRootPath = app.Environment.WebRootPath;
if (string.IsNullOrWhiteSpace(webRootPath))
{
	webRootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
}

Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "portfolio"));
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "client-galleries"));
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "client-galleries", "previews"));
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads", "client-galleries", "originals"));

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "logs"));

await ServicesDataSeeder.SeedAsync(app.Services);

app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
	using var scope = app.Services.CreateScope();
	await AppDataSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseRateLimiter();

app.UseMiddleware<CsrfProtectionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();