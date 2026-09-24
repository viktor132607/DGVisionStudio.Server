using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Application.DTOs.Pagination;
using DGVisionStudio.Domain.Entities;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DGVisionStudio.Api.Services;

public sealed class AdminUserService : IAdminUserService
{
    private readonly AdminUserQueryService queries;
    private readonly AdminUserSeenService seen;
    private readonly AdminUserRoleService roles;
    private readonly AdminUserAccountService accounts;

    [ActivatorUtilitiesConstructor]
    public AdminUserService(
        AdminUserQueryService queries,
        AdminUserSeenService seen,
        AdminUserRoleService roles,
        AdminUserAccountService accounts)
    {
        this.queries = queries;
        this.seen = seen;
        this.roles = roles;
        this.accounts = accounts;
    }

    public AdminUserService(
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IConfiguration configuration)
    {
        var protection = new AdminUserProtectedAccountPolicy(configuration);

        queries = new AdminUserQueryService(
            userManager,
            db,
            protection);
        seen = new AdminUserSeenService(userManager);
        roles = new AdminUserRoleService(
            userManager,
            protection);
        accounts = new AdminUserAccountService(
            userManager,
            protection);
    }

    public Task<ControllerServiceResult> GetUsersAsync(PagedQueryDto query) =>
        queries.GetUsersAsync(query);

    public Task<ControllerServiceResult> MarkAllSeenAsync() =>
        seen.MarkAllSeenAsync();

    public Task<ControllerServiceResult> GetUserAlbumsAsync(string id) =>
        queries.GetUserAlbumsAsync(id);

    public Task<ControllerServiceResult> MakeAdminAsync(string id) =>
        roles.MakeAdminAsync(id);

    public Task<ControllerServiceResult> RemoveAdminAsync(string id) =>
        roles.RemoveAdminAsync(id);

    public Task<ControllerServiceResult> BlockUserAsync(string id) =>
        accounts.BlockUserAsync(id);

    public Task<ControllerServiceResult> UnblockUserAsync(string id) =>
        accounts.UnblockUserAsync(id);

    public Task<ControllerServiceResult> DeleteUserAsync(string id) =>
        accounts.DeleteUserAsync(id);
}
