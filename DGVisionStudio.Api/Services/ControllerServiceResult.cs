using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace DGVisionStudio.Api.Services;

public sealed record ControllerServiceResult(int StatusCode, object? Value = null)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;

    public static ControllerServiceResult Ok(object? value = null) =>
        new(StatusCodes.Status200OK, value);

    public static ControllerServiceResult NoContent() =>
        new(StatusCodes.Status204NoContent);

    public static ControllerServiceResult BadRequest(object? value) =>
        new(StatusCodes.Status400BadRequest, value);

    public static ControllerServiceResult Unauthorized(object? value) =>
        new(StatusCodes.Status401Unauthorized, value);

    public static ControllerServiceResult Forbidden() =>
        new(StatusCodes.Status403Forbidden);

    public static ControllerServiceResult NotFound(object? value = null) =>
        new(StatusCodes.Status404NotFound, value);

    public static ControllerServiceResult Locked(object? value) =>
        new(StatusCodes.Status423Locked, value);

    public static ControllerServiceResult Error(object? value) =>
        new(StatusCodes.Status500InternalServerError, value);
}

public sealed record AdminRequestContext(
    string UserId,
    string Email,
    string DisplayName,
    string? RemoteIpAddress,
    string UserAgent,
    string TraceId);

public sealed record AuthRequestContext(
    ClaimsPrincipal User,
    string TraceId);

public sealed record FileDownloadResult(
    Stream Stream,
    string ContentType,
    string FileName);

public sealed record PhysicalFileDownloadResult(
    string Path,
    string ContentType,
    string FileName,
    Func<Task> CleanupAsync);

public sealed record StreamingFileDownloadResult(
    string ContentType,
    string FileName,
    Func<Stream, CancellationToken, Task> WriteAsync);
