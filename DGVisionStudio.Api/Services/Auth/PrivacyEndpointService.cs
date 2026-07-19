using System.Security.Claims;
using DGVisionStudio.Api.Models;
using DGVisionStudio.Api.Services.Interfaces;
using DGVisionStudio.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DGVisionStudio.Api.Services;

public sealed class PrivacyEndpointService : IPrivacyEndpointService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPrivacyService _privacyService;

    public PrivacyEndpointService(
        UserManager<ApplicationUser> userManager,
        IPrivacyService privacyService)
    {
        _userManager = userManager;
        _privacyService = privacyService;
    }

    public async Task<ControllerServiceResult> ExportAsync(
        ClaimsPrincipal principal,
        string traceId)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            return Unauthorized(traceId);

        var export = await _privacyService.ExportUserDataAsync(user.Id);
        return export is null
            ? NotFound(traceId)
            : ControllerServiceResult.Ok(export);
    }

    public async Task<ControllerServiceResult> DeleteAccountAsync(
        ClaimsPrincipal principal,
        bool confirmed,
        string traceId)
    {
        if (!confirmed)
        {
            return ControllerServiceResult.BadRequest(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Code = ApiErrorCodes.ValidationError,
                Message = "Account deletion must be explicitly confirmed.",
                TraceId = traceId
            });
        }

        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
            return Unauthorized(traceId);

        return await _privacyService.AnonymizeUserDataAsync(user.Id)
            ? ControllerServiceResult.NoContent()
            : NotFound(traceId);
    }

    private static ControllerServiceResult Unauthorized(string traceId) =>
        ControllerServiceResult.Unauthorized(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            Code = ApiErrorCodes.Unauthorized,
            Message = "Unauthorized.",
            TraceId = traceId
        });

    private static ControllerServiceResult NotFound(string traceId) =>
        ControllerServiceResult.NotFound(new ApiErrorResponse
        {
            StatusCode = StatusCodes.Status404NotFound,
            Code = ApiErrorCodes.NotFound,
            Message = "Resource not found.",
            TraceId = traceId
        });
}
