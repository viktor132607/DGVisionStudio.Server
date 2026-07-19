using DGVisionStudio.Application.DTOs;

namespace DGVisionStudio.Api.Services.Interfaces;

public interface IAuthService
{
    Task<ControllerServiceResult> RegisterAsync(RegisterRequest model, string traceId);
    string GetConfirmEmailRedirectUrl();
    Task<ControllerServiceResult> LoginAsync(LoginRequest model, string traceId);
    Task<ControllerServiceResult> LogoutAsync(AuthRequestContext context);
    Task<ControllerServiceResult> GetCurrentUserAsync(AuthRequestContext context);
    Task<ControllerServiceResult> ForgotPasswordAsync(ForgotPasswordRequest model, string traceId);
    string GetResetPasswordRedirectUrl(string email, string token);
    Task<ControllerServiceResult> ResetPasswordAsync(ResetPasswordRequest model, string traceId);
    Task<ControllerServiceResult> ChangePasswordAsync(ChangePasswordRequest model, AuthRequestContext context);
}
