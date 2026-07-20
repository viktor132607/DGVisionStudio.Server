using DGVisionStudio.Api.Controllers;
using DGVisionStudio.Infrastructure.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DGVisionStudio.Tests.Controllers;

public sealed class RemainingControllerAttributeContractTests
{
    public static TheoryData<Type, string, bool, string?> Contracts => new()
    {
        { typeof(AccountController), "api/account", true, null },
        { typeof(AdminAuditLogsController), "api/admin/audit-logs", true, "Admin" },
        { typeof(AdminCalendarController), "api/admin/calendar", true, "Admin" },
        { typeof(AdminClientGalleriesArchiveController), "api/admin/client-galleries", true, "Admin" },
        { typeof(AdminClientGalleriesDownloadController), "api/admin/client-galleries", true, "Admin" },
        { typeof(AdminClientGalleryAccessController), "api/admin/client-galleries/{galleryId:int}/access", true, "Admin" },
        { typeof(AdminClientGalleryMediaController), "api/admin/client-galleries/{galleryId:int}/media", true, "Admin" },
        { typeof(AdminClientGalleryPhotosController), "api/admin/client-galleries/{galleryId:int}", true, "Admin" },
        { typeof(AdminContactRequestsController), "api/admin/contact-requests", true, "Admin" },
        { typeof(AdminDashboardController), "api/admin/dashboard", true, "Admin" },
        { typeof(AdminNotificationsController), "api/admin/notifications", true, "Admin" },
        { typeof(AdminPricingController), "api/admin/pricing", true, "Admin" },
        { typeof(AdminPrintRequestsController), "api/admin/print-requests", true, "Admin" },
        { typeof(ClientPrintRequestsController), "api/client/print-requests", true, null },
        { typeof(CsrfController), "api/csrf", false, null },
        { typeof(DebugUsersController), "api/debug/users", false, null },
        { typeof(HomeController), "api/home", false, null },
        { typeof(PricingController), "api/pricing", false, null },
        { typeof(SiteSettingsController), "api/site-settings", false, null }
    };

    [Theory]
    [MemberData(nameof(Contracts))]
    public void Controller_PreservesApiRouteAndAuthorization(
        Type controllerType,
        string route,
        bool requiresAuthorization,
        string? role)
    {
        controllerType.GetCustomAttributes(typeof(ApiControllerAttribute), inherit: true)
            .Should().ContainSingle();
        controllerType.GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Should().ContainSingle()
            .Which.Template.Should().Be(route);

        var authorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        if (!requiresAuthorization)
        {
            authorize.Should().BeEmpty();
            return;
        }

        authorize.Should().ContainSingle();
        authorize.Single().Roles.Should().Be(role);
    }
}
