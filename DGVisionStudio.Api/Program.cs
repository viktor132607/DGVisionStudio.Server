using DGVisionStudio.Api.Configuration;
using DGVisionStudio.Api.Middleware;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using DGVisionStudio.Infrastructure.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

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

var uploadOptions = builder.Configuration
	.GetSection(UploadOptions.SectionName)
	.Get<UploadOptions>() ?? new UploadOptions();

var storageOptions = builder.Configuration
	.GetSection(StorageOptions.SectionName)
	.Get<StorageOptions>() ?? new StorageOptions();

var frontendOptions = builder.Configuration
	.GetSection(FrontendOptions.SectionName)
	.Get<FrontendOptions>() ?? new FrontendOptions();

builder.Services.AddOptions<UploadOptions>()
	.Bind(builder.Configuration.GetSection(UploadOptions.SectionName))
	.Validate(options => options.MaxFileSizeBytes > 0, "Upload:MaxFileSizeBytes must be greater than 0.")
	.Validate(options => options.MaxFilesPerRequest > 0, "Upload:MaxFilesPerRequest must be greater than 0.")
	.ValidateOnStart();

builder.Services.AddOptions<StorageOptions>()
	.Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
	.Validate(options => string.IsNullOrWhiteSpace(options.Provider)
		|| string.Equals(options.Provider, "FileSystem", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(options.Provider, "Cloudinary", StringComparison.OrdinalIgnoreCase),
		"Storage:Provider must be empty, FileSystem, or Cloudinary.")
	.ValidateOnStart();

builder.Services.AddOptions<FrontendOptions>()
	.Bind(builder.Configuration.GetSection(FrontendOptions.SectionName))
	.Validate(options => FrontendOptions.IsValidOrigin(options.Url), "Frontend:Url must be an absolute http/https URL when configured.")
	.Validate(options => options.AdditionalOrigins.All(FrontendOptions.IsValidOrigin), "Frontend:AdditionalOrigins entries must be absolute http/https URLs.")
	.ValidateOnStart();

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.WebHost.ConfigureKestrel(options =>
{
	options.Limits.MaxRequestBodySize = uploadOptions.MaxRequestSizeBytes;
});

builder.Services.AddControllers()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
	});

builder.Services.Configure<FormOptions>(options =>
{
	options.MultipartBodyLengthLimit = uploadOptions.MaxRequestSizeBytes;
	options.ValueLengthLimit = int.MaxValue;
	options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

builder.Services.AddDGVisionApplicationServices(storageOptions);

var resolvedDatabaseConnection = DatabaseConnectionStringResolver.Resolve(builder.Configuration, builder.Environment);

builder.Services.AddDbContext<AppDbContext>(options =>
	options.UseNpgsql(resolvedDatabaseConnection.ConnectionString));

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

var allowedOrigins = frontendOptions.GetAllowedOrigins();

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

app.Logger.LogInformation(
	"PostgreSQL connection resolved from configuration key {DatabaseConnectionSource}.",
	resolvedDatabaseConnection.SourceKey);

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

await CalendarReminderSchemaSetup.EnsureAsync(app.Services);
await PortfolioMediaNameSetup.EnsureAsync(app.Services);
await ServicesDataSeeder.SeedAsync(app.Services);
await PricingDataSeeder.SeedAsync(app.Services);

app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
	using var scope = app.Services.CreateScope();
	await AppDataSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
	app.MapScalarApiReference(options =>
	{
		options.WithTheme(ScalarTheme.Moon)
			.WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl);
	});

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